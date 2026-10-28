namespace SimpleRPG.UI
{
    /// <summary>
    /// A piece of the HUD or a menu that shows the local player's character. <see cref="PlayerUIController"/> binds every view beneath it when
    /// the local player is announced (see <see cref="LocalPlayer"/>) and unbinds them when it goes, so a view is added by implementing this and
    /// the character never needs to know about it.
    /// </summary>
    public interface ILocalPlayerView
    {
        /// <summary>Starts showing the character.</summary>
        void BindLocalPlayer(Character character);

        /// <summary>Stops showing the character it was bound to.</summary>
        void UnbindLocalPlayer();
    }
}
