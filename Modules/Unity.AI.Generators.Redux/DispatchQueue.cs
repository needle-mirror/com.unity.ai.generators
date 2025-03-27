using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.AI.Generators.Redux
{
    class DispatchQueue
    {
        static int s_MaxDispatchStack = 10;

        readonly List<Action> m_Queue = new();
        int m_DispatchStackSize;

        public void Queue(Store store, StandardAction action, params string[] slices) =>
            m_Queue.Add(() => store.DispatchToSlices(action, slices));

        public void Drain(StandardAction sourceAction)
        {
            m_DispatchStackSize++;
            if (m_DispatchStackSize >= s_MaxDispatchStack)
            {
                Debug.LogError($"Dispatch has found a possible infinite loop. "
                    + $"Further processing of this action will be cancelled: {sourceAction.type}\n"
                    + "Possible causes include having a selector's result not compared correctly because the result is a new object. "
                    + "Like an different IEnumerable with identical content.");
                return;
            }

            var clone = m_Queue.ToList();
            m_Queue.Clear();
            foreach (var action in clone)
                action();

            m_DispatchStackSize--;
        }
    }
}
