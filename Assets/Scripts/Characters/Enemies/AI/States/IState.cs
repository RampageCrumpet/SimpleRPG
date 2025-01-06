using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Interface to be used by our states.
/// </summary>
public interface IState
{
    /// <summary>
    /// Set up the state. Called whenever we enter a state.
    /// </summary>
    public void OnStateEnter();

    /// <summary>
    /// Update the state.
    /// </summary>
    public void OnStateUpdate();

    /// <summary>
    /// Finish off the state. Called whenever we move to a new state.
    /// </summary>
    public void OnStateExit();
}
