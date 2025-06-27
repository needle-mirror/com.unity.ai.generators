using System;

namespace Unity.AI.Generators.UI.Utilities
{
    interface IInterruptedDownloadData
    {
        int progressTaskId { get; }
        string uniqueTaskId { get; }
    }
}