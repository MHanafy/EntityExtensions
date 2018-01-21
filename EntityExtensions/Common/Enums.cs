namespace EntityExtensions.Common
{
    public enum RefreshMode
    {
        /// <summary>
        ///  Doesn't read back database generated columns, More efficient when updated values aren't needed.
        /// </summary>
        None,
        /// <summary>
        /// Reads identity columns only and updates entities accordingly
        /// </summary>
        Identity,
        /// <summary>
        /// Reads both identity and computed columns, and updates entities accordingly.
        /// </summary>
        All
    }
}
