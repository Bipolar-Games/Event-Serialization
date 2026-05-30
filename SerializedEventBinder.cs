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
    [ExecuteAlways]
    public class SerializedEventBinder : MonoBehaviour
    {
        private readonly List<ISerializedEventHost> serializedEventHosts = new List<ISerializedEventHost>();

        private void Reset()
        {
            //hideFlags = HideFlags.HideInInspector;
#if UNITY_EDITOR
            UnityEditor.ObjectChangeEvents.changesPublished += ObjectChangeEvents_changesPublished;
#endif
        }

        private void Awake()
        {
            if (Application.isPlaying)
            {
                GetComponents(serializedEventHosts);
                foreach (var host in serializedEventHosts)
                {
                    host.BindEvents();
                }
            }
        }

#if UNITY_EDITOR
        private void ObjectChangeEvents_changesPublished(ref UnityEditor.ObjectChangeEventStream stream)
        {
            if (HasGameObjectStructureChanged(stream))
            {
                HandleStructureChange();
            }
        }

        private bool HasGameObjectStructureChanged(UnityEditor.ObjectChangeEventStream stream)
        {
            for (int i = 0; i < stream.length; i++)
                if (stream.GetEventType(i) == UnityEditor.ObjectChangeKind.ChangeGameObjectStructure)
                    return true;

            return false;
        }

        private void HandleStructureChange()
        {
            var hosts = GetComponents<ISerializedEventHost>();
            if (hosts.Length <= 0)
            {
                DestroyImmediate(this);
                return;
            }

            if (Application.isPlaying)
            {
                for (int i = serializedEventHosts.Count - 1; i >= 0; i--)
                    if (serializedEventHosts[i] == null)
                        serializedEventHosts.RemoveAt(i);

                foreach (var host in hosts)
                {
                    if (serializedEventHosts.IndexOf(host) < 0)
                    {
                        serializedEventHosts.Add(host);
                        host.BindEvents();
                    }
                }
            }
        }
#endif

        private void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.ObjectChangeEvents.changesPublished -= ObjectChangeEvents_changesPublished;
            UnityEditor.ObjectChangeEvents.changesPublished += ObjectChangeEvents_changesPublished;
#endif
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            UnityEditor.ObjectChangeEvents.changesPublished -= ObjectChangeEvents_changesPublished;
#endif
        }
    }

#if UNITY_EDITOR
    namespace Editor
    {
        [UnityEditor.CustomEditor(typeof(SerializedEventBinder))]
        internal class SerializedEventBinderEditor : UnityEditor.Editor
        {
            public override void OnInspectorGUI() { }
        }
    }
#endif
}
