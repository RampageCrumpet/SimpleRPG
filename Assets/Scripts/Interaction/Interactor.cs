using UnityEngine;
using SimpleRPG.Networking;
using SimpleRPG.ObjectInteractions;
using Unity.Netcode;

namespace SimpleRPG
{
    /// <summary>
    /// A character's use of the world: finding what it is looking at and interacting with it. The server owns the interaction. The player asks to
    /// start and to stop; the server checks the target is reachable, times the interaction (faster with Interaction Speed), cancels it if the
    /// character walks off or dies, and applies its effect when the time is up. The player's machine only shows the countdown. With no network
    /// (a test, an offline scene) the interactor is its own authority and does all of this itself.
    /// </summary>
    public class Interactor : NetworkBehaviour
    {
        /// <summary>
        /// The interactable object that the player is currently interacting with. Is null if the player is not interacting with anything.
        /// </summary>
        public Interactable Interactable { get; private set; }

        /// <summary>
        /// The maximum distance in unity units the player can move from the interaction location before the interaction is cancelled.
        /// </summary>
        private const float MaximumMoveDistance = 0.5f;

        /// <summary>
        /// How far beyond an interactable's range the server still lets a request start, to absorb the difference between what the player's
        /// machine and the server saw.
        /// </summary>
        private const float ServerRangeSlack = 2f;

        /// <summary>The slowest an interaction can be timed, so a stat that falls to nothing cannot make one last forever.</summary>
        private const float MinimumSpeed = 0.1f;

        /// <summary>
        /// The source location of the interaction.
        /// </summary>
        private Vector3 interactionLocation;

        /// <summary>
        /// The time left before the interaction takes effect. On the server it is the real clock; on the player's machine it is a display that the
        /// server's answer sets.
        /// </summary>
        public float InteractionTimeRemaining { get; private set; }

        /// <summary>How long the current interaction takes in all, after Interaction Speed. Zero when not interacting.</summary>
        public float InteractionDuration { get; private set; }

        private bool isInteracting;

        /// <summary>
        /// Has the interaction taken effect? (Server only.)
        /// </summary>
        private bool isInteractionActive;

        private Character character;

        private readonly RequestThrottle beginThrottle = new RequestThrottle(perSecond: 5d, burst: 10d);

        /// <summary>The character this interacts for.</summary>
        public Character Character => character;

        /// <summary>
        /// True while the character may interact with the world. A dead character cannot.
        /// </summary>
        public bool CanInteract => character == null || character.IsAlive;

        /// <summary>
        /// Is this Interactor currently interacting with something.
        /// </summary>
        public bool IsInteracting => isInteracting && Interactable != null;

        // The server, or no network at all: this machine decides.
        private bool HasAuthority => !IsSpawned || IsServer;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            character = GetComponent<Character>();
            character.OnDeath += StopInteraction;
        }

        // Update is called once per frame
        private void Update()
        {
            if (!isInteracting)
            {
                return;
            }

            if (!HasAuthority)
            {
                // The player's machine only counts down what the server said.
                InteractionTimeRemaining -= Time.deltaTime;
                return;
            }

            // The target was destroyed, or the character walked away from where it started.
            if (Interactable == null || (transform.position - interactionLocation).magnitude > MaximumMoveDistance)
            {
                StopOnAuthority();
                return;
            }

            InteractionTimeRemaining -= Time.deltaTime;

            // Check to see if we've completed interacting with the object.
            if (InteractionTimeRemaining <= 0 && !isInteractionActive && CanInteract)
            {
                isInteractionActive = true;
                Interactable.ActivateInteraction(this);
            }
        }

        /// <summary>
        /// Start interacting with the interactable. On a player's machine this asks the server, which may refuse (too far, out of reach).
        /// </summary>
        /// <param name="interactable"> The <see cref="ObjectInteractions.Interactable"/> we want to start interacting with.</param>
        public void StartInteraction(Interactable interactable)
        {
            if (!CanInteract || interactable == null)
            {
                return;
            }

            if (HasAuthority)
            {
                BeginOnAuthority(interactable);
                return;
            }

            if (!IsOwner || !interactable.TryGetHandle(out NetworkObjectReference anchor, out int id))
            {
                return;
            }

            // Show the countdown at once with the interaction's own time; the server's answer corrects it for Interaction Speed.
            SetState(interactable, interactable.InteractionTime);
            BeginRpc(anchor, id);
        }

        /// <summary>
        /// Stop interacting. On a player's machine this tells the server, which ends it and tells the interactable.
        /// </summary>
        public void StopInteraction()
        {
            if (HasAuthority)
            {
                StopOnAuthority();
                return;
            }

            if (!IsOwner || Interactable == null)
            {
                return;
            }

            ClearState();
            StopRpc();
        }

        // ----- The server's side -----

        private void BeginOnAuthority(Interactable interactable)
        {
            if (!CanReach(interactable))
            {
                RefuseToOwner();
                return;
            }

            // One interaction at a time.
            if (Interactable != null)
            {
                StopOnAuthority();
            }

            float duration = interactable.InteractionTime / Speed();
            SetState(interactable, duration);
            interactionLocation = transform.position;
            if (IsSpawned && !IsOwner)
            {
                BeganRpc(duration);
            }
        }

        private bool CanReach(Interactable interactable)
        {
            return CanInteract && interactable.isActiveAndEnabled && interactable.CanBeInteractedWith &&
                   Vector3.Distance(transform.position, interactable.transform.position) <= interactable.InteractionRange + ServerRangeSlack;
        }

        private float Speed()
        {
            float speed = character != null && character.Stats != null ? character.Stats.InteractionSpeed : 1f;
            return Mathf.Max(MinimumSpeed, speed);
        }

        private void StopOnAuthority()
        {
            if (Interactable == null)
            {
                ClearState();
                return;
            }

            Interactable finished = Interactable;
            ClearState();
            finished.FinishInteraction(this);
            if (IsSpawned && !IsOwner)
            {
                EndedRpc();
            }
        }

        // The server turned a request down, so the machine that showed a countdown for it stops.
        private void RefuseToOwner()
        {
            if (IsSpawned && !IsOwner)
            {
                EndedRpc();
            }
        }

        private void SetState(Interactable interactable, float duration)
        {
            Interactable = interactable;
            isInteracting = true;
            isInteractionActive = false;
            InteractionDuration = duration;
            InteractionTimeRemaining = duration;
        }

        private void ClearState()
        {
            Interactable = null;
            isInteracting = false;
            isInteractionActive = false;
            InteractionDuration = 0f;
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void BeginRpc(NetworkObjectReference anchor, int id, RpcParams rpcParams = default)
        {
            if (!beginThrottle.TryAllow(rpcParams.Receive.SenderClientId, Time.unscaledTimeAsDouble))
            {
                RefuseToOwner();
                return;
            }

            if (Interactable.TryResolve(anchor, id, out Interactable interactable))
            {
                BeginOnAuthority(interactable);
            }
            else
            {
                RefuseToOwner();
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void StopRpc() => StopOnAuthority();

        // ----- The player's machine -----

        [Rpc(SendTo.Owner)]
        private void BeganRpc(float duration)
        {
            if (!HasAuthority)
            {
                InteractionDuration = duration;
                InteractionTimeRemaining = duration;
            }
        }

        [Rpc(SendTo.Owner)]
        private void EndedRpc()
        {
            if (!HasAuthority)
            {
                ClearState();
            }
        }

        /// <summary>
        /// What the player is looking at and could interact with, refreshed once a frame by the owning player's controls. Null if nothing.
        /// </summary>
        public Interactable Target { get; private set; }

        /// <summary>
        /// Looks for the interactable the player is looking at and remembers it as <see cref="Target"/>.
        /// </summary>
        public void RefreshTarget()
        {
            Target = FindInteractionTarget();
        }

        /// <summary>
        /// Finds the interactable object that the player is looking at within range and returns it if one exists.
        /// </summary>
        /// <returns> Returns the interactable object the player is looking at within range if one exists, otherwise returns null.</returns>
        public Interactable FindInteractionTarget()
        {
            if (!CanInteract) return null;

            Camera mainCamera = Camera.main;
            if (mainCamera == null) return null;
            Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                // Only interactables above the collider that was hit can be looked at through it, so walk up from it
                // instead of testing every interactable in the scene every frame.
                for (Transform node = hit.collider.transform; node != null; node = node.parent)
                {
                    if (node.TryGetComponent(out Interactable interactable) && interactable.isActiveAndEnabled && interactable.CanBeInteractedWith &&
                        interactable.IsLookedAtVia(hit.collider) && Vector3.Distance(transform.position, interactable.transform.position) <= interactable.InteractionRange)
                    {
                        return interactable;
                    }
                }
            }

            return null;
        }

        public override void OnDestroy()
        {
            if (character != null)
            {
                character.OnDeath -= StopInteraction;
            }

            base.OnDestroy();
        }
    }
}
