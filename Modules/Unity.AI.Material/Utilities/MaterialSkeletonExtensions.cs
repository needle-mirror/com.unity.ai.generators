using Unity.AI.Material.Services.Stores.States;
using UnityEngine;

namespace Unity.AI.Material.Services.Utilities
{
    record MaterialSkeleton(int taskID, int counter) : MaterialResult(FromPreview(new TextureSkeleton(taskID, counter)));
}
