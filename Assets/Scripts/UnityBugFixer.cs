
using UnityEngine;
using Unity.MLAgents.Actuators;
using System.Reflection;

public class UnityBugFixer : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void FixEmptySegments()
    {
        var fieldInt = typeof(ActionSegment<int>).GetField("Empty", BindingFlags.Public | BindingFlags.Static);
        var emptyInt = new ActionSegment<int>(new int[0], 0, 0);
        if (fieldInt != null)
        {
            fieldInt.SetValue(null, emptyInt);
            Debug.Log("Fixed ActionSegment<int>.Empty");
        }
        
        var fieldFloat = typeof(ActionSegment<float>).GetField("Empty", BindingFlags.Public | BindingFlags.Static);
        var emptyFloat = new ActionSegment<float>(new float[0], 0, 0);
        if (fieldFloat != null)
        {
            fieldFloat.SetValue(null, emptyFloat);
            Debug.Log("Fixed ActionSegment<float>.Empty");
        }

        var fieldBuffers = typeof(ActionBuffers).GetField("Empty", BindingFlags.Public | BindingFlags.Static);
        if (fieldBuffers != null)
        {
            fieldBuffers.SetValue(null, new ActionBuffers(emptyFloat, emptyInt));
            Debug.Log("Fixed ActionBuffers.Empty");
        }
    }
}

