namespace SmartWorkerRestock
{
    /// <summary>Read by the Configuration Manager mod (by name, through reflection) to adjust how a setting is shown.</summary>
#pragma warning disable 0649
    internal sealed class ConfigurationManagerAttributes
    {
        /// <summary>Show a 0-100 range as a percentage (Configuration Manager does it by default for such ranges).</summary>
        public bool? ShowRangeAsPercent;
    }
#pragma warning restore 0649
}
