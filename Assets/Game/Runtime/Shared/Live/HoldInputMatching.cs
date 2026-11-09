using System;

namespace OpenSekai.Shared.Live
{
    // Augment an existing assignment without allocating. Keep existing owners unless moving one
    // to another eligible input is necessary to let an additional hold receive input this frame.
    internal static class HoldInputMatching
    {
        internal static bool TryAssign(int note, int inputCount, Func<int, int, bool> eligible,
            int[] inputNotes, bool[] visited)
        {
            for (int input = 0; input < inputCount; input++)
            {
                if (!visited[input] && inputNotes[input] < 0 && eligible(note, input))
                {
                    inputNotes[input] = note;
                    return true;
                }
            }
            for (int input = 0; input < inputCount; input++)
            {
                if (visited[input] || !eligible(note, input)) continue;
                visited[input] = true;
                int previous = inputNotes[input];
                if (previous >= 0 && TryAssign(previous, inputCount, eligible, inputNotes, visited))
                {
                    inputNotes[input] = note;
                    return true;
                }
            }
            return false;
        }
    }
}
