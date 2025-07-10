
using System;

namespace Onova.Services
{
    /// <summary>
    /// 
    /// </summary>
    public interface IMultiProgressBar
    {
        /// <summary>
        /// Create a progress bar to show in a new line
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        IProgress<double>? CreateProgressBar(string name, string description = "");

        /// <summary>
        /// Called to create a global bar with a space over it
        /// </summary>
        void CreateGlobalProgressBar();
    }
}
