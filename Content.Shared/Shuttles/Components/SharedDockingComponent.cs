namespace Content.Shared.Shuttles.Components
{
    public abstract partial class SharedDockingComponent : Component
    {
        // Yes I left this in for now because there's no overhead and we'll need a client one later anyway
        // and I was too lazy to delete it.

        public abstract bool Docked { get; }

        // Harmony
        /// <summary>
        /// True if there is currently a grid in FTL trying to dock here.
        /// </summary>
        [DataField]
        public bool QueuedDocked = false;
        // End Harmony
    }
}
