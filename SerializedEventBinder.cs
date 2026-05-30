using System.Collections.Generic;
using UnityEngine;

namespace Bipolar.EventSerialization
{
    public interface ISerializedEventHost
    {
        void BindEvents();
    }

    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class SerializedEventBinder : MonoBehaviour
    {
        private readonly List<ISerializedEventHost> serializedEventHosts = new List<ISerializedEventHost>();

        private void Reset()
        {
            //hideFlags = HideFlags.HideInInspector;
        }

        private void Awake()
        {
            GetComponents(serializedEventHosts);
            foreach (var host in serializedEventHosts)
            {
                host.BindEvents();
            }
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(SerializedEventBinder))]
    internal class SerializedEventBinderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() { }
    }
#endif

}
