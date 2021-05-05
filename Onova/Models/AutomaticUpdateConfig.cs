namespace Onova.Models
{
    /// <summary>
    /// Configuration for Autoupdating
    /// </summary>
    public class AutomaticUpdateConfig
    {
        /// <summary>
        /// Autoupdate can be turned off from config
        /// </summary>
        public bool Active { get; set; } = false;

        /// <summary>
        /// Max version which can be considered auto upgradable, we can avoid unwanted upgrades over braking changes
        /// </summary>
        public string MaxUpdatableVersion { get; set; } = "99999.99999.99999.99999";
    }
}