using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// State machine that can execute a series of <see cref="IState"/> objects.
/// </summary>
public class StateMachine
{
    private IState currentState;

    /// <summary>
    /// Start the <see cref="StateMachine"/> with the given <see cref="IState"/>.
    /// </summary>
    /// <param name="currentState"> The <see cref="IState"/> we want our StateMachine to start with.<</param>
    public void InitializeStateMachine(IState currentState)
    {
        if (currentState == null) throw new System.ArgumentNullException(nameof(currentState));
        this.currentState = currentState;
        currentState.OnStateEnter();
    }

    
    public void Update()
    {
        currentState?.OnStateUpdate();
    }

    /// <summary>
    /// Change the currently executing <see cref="IState"/>.
    /// </summary>
    /// <param name="newState"> The new <see cref="IState"/> we want to execute.</param>
    public void ChangeState(IState newState)
    {
        if (newState == null) throw new System.ArgumentNullException(nameof(newState));
        if (currentState == newState) return;
        currentState?.OnStateExit();
        currentState = newState;
        currentState.OnStateEnter();
    }

}
