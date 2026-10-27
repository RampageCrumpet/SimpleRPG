using SimpleRPG.Appearance;
using UnityEngine;
using UnityEngine.UI;

namespace SimpleRPG.UI
{
    /// <summary>
    /// Shows the local character in the inventory, since the first-person camera never lets the player see themselves.
    /// A copy of the bare rig stands on a stage far from the world, wears the same parts as the character (read from <see cref="CharacterAppearance"/>),
    /// plays the character's animator in its idle state, and a camera renders it into the image. It draws only while the screen is open.
    /// Armor is not drawn yet: worn parts come from the appearance system, which armor does not feed (see the fitted armor visuals bead).
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class CharacterPreviewUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The bare rig prefab (skeleton, no parts, no animator).")]
        private GameObject rigPrefab;

        [SerializeField]
        [Tooltip("Size of the render texture.")]
        private Vector2Int textureSize = new Vector2Int(520, 704);

        [SerializeField]
        [Tooltip("Where the stage is, far from anything the world camera can see.")]
        private Vector3 stagePosition = new Vector3(0f, -5000f, 0f);

        [SerializeField]
        [Tooltip("Height of the point the camera looks at, up the body.")]
        private float lookHeight = 0.88f;

        [SerializeField]
        [Tooltip("How far in front of the character the camera stands.")]
        private float cameraDistance = 3.4f;

        [SerializeField]
        [Tooltip("Vertical field of view of the preview camera.")]
        private float fieldOfView = 30f;

        private CharacterAppearance appearance;
        private GameObject stage;
        private Camera previewCamera;
        private RenderTexture texture;
        private AppearanceView view;

        /// <summary>
        /// True once a character is bound and the stage exists.
        /// </summary>
        public bool IsBuilt => stage != null;

        /// <summary>
        /// The camera that renders the preview, or null before a character is bound.
        /// </summary>
        public Camera PreviewCamera => previewCamera;

        /// <summary>
        /// Shows this character. Pass null to remove the preview.
        /// </summary>
        public void Bind(Character character)
        {
            Unbind();

            if (character == null || rigPrefab == null)
            {
                return;
            }

            appearance = character.GetComponent<CharacterAppearance>();
            if (appearance == null || appearance.RigRoot == null)
            {
                return;
            }

            Build(character);
            appearance.Changed += Rebuild;
            Rebuild();
            stage.SetActive(isActiveAndEnabled);
        }

        private void Build(Character character)
        {
            stage = new GameObject("CharacterPreviewStage") { hideFlags = HideFlags.DontSave };
            stage.transform.position = stagePosition;

            GameObject rig = Instantiate(rigPrefab, stage.transform);
            rig.transform.localPosition = Vector3.zero;
            rig.transform.localRotation = Quaternion.identity;

            Transform partsRoot = new GameObject("Parts").transform;
            partsRoot.SetParent(rig.transform, false);
            Transform rigRoot = rig.transform.Find(appearance.RigRoot.name);
            view = new AppearanceView(new RigBoneIndex(rigRoot != null ? rigRoot : rig.transform), partsRoot);

            // The same animator setup as the character, so the preview stands in its idle pose.
            Animator source = character.GetComponent<Animator>();
            if (source != null)
            {
                Animator animator = rig.AddComponent<Animator>();
                animator.runtimeAnimatorController = source.runtimeAnimatorController;
                animator.avatar = source.avatar;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            texture = new RenderTexture(textureSize.x, textureSize.y, 24, RenderTextureFormat.ARGB32) { name = "CharacterPreview" };
            texture.Create();

            GameObject cameraObject = new GameObject("PreviewCamera");
            cameraObject.transform.SetParent(stage.transform, false);
            // The rig faces +Z, so the camera stands in front of it on +Z and looks back.
            cameraObject.transform.localPosition = new Vector3(0f, lookHeight, cameraDistance);
            cameraObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCamera.fieldOfView = fieldOfView;
            previewCamera.nearClipPlane = 0.1f;
            previewCamera.farClipPlane = 20f;
            previewCamera.targetTexture = texture;
            previewCamera.depth = -100f;

            // A light that reaches only the stage: the world is thousands of units away.
            GameObject lightObject = new GameObject("PreviewLight");
            lightObject.transform.SetParent(stage.transform, false);
            lightObject.transform.localPosition = new Vector3(1.5f, 2.2f, 2.5f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 12f;
            light.intensity = 2f;

            RawImage image = GetComponent<RawImage>();
            image.texture = texture;
            image.color = Color.white;
        }

        /// <summary>
        /// Shows the same parts the character wears now.
        /// </summary>
        private void Rebuild()
        {
            if (view == null || appearance == null)
            {
                return;
            }

            view.ClearAll();
            foreach (BodyPartDefinition part in appearance.CurrentParts)
            {
                view.Show(part);
            }
        }

        private void Unbind()
        {
            if (appearance != null)
            {
                appearance.Changed -= Rebuild;
                appearance = null;
            }

            if (stage != null)
            {
                Destroy(stage);
                stage = null;
            }

            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
                texture = null;
            }

            view = null;
            previewCamera = null;
        }

        private void OnEnable()
        {
            if (stage != null)
            {
                stage.SetActive(true);
            }
        }

        private void OnDisable()
        {
            if (stage != null)
            {
                stage.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
