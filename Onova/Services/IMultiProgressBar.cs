
using System;

namespace Onova.Services
{
    public interface IMultiProgressBar
    {
        IProgress<double> CreateProgressBar(string name, string description = "");

        void CreateGlobalProgressBar();
    }
}
