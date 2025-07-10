using System.Collections.Generic;
using UnityEngine;
#if MTE_NDMF
using nadena.dev.ndmf.preview;
#endif

namespace net.nekobako.MaskTextureEditor.Editor
{
    internal class TextureUndoStack : ScriptableObject
    {
        private class Counter : ScriptableObject
        {
            [SerializeField]
            private int m_Count = 0;

            public int Count
            {
                get => m_Count;
                set => m_Count = value;
            }

            private void Awake()
            {
                // Set HideFlags to keep the state when reloading a scene or domain
                hideFlags = HideFlags.HideAndDontSave;
            }
        }

        [SerializeField]
        private RenderTexture m_Target = null!; // Initialize in Init

        [SerializeField]
        private List<Texture2D> m_Stack = null!; // Initialize in Init

        [SerializeField]
        private Counter m_Counter = null!; // Initialize in Init

        public bool CanUndo => m_Counter.Count > 1;
        public bool CanRedo => m_Counter.Count < m_Stack.Count;

        private void Awake()
        {
            // Set HideFlags to keep the state when reloading a scene or domain
            hideFlags = HideFlags.HideAndDontSave;
        }

        private void OnEnable()
        {
            UnityEditor.Undo.undoRedoPerformed += Apply;
        }

        private void OnDisable()
        {
            UnityEditor.Undo.undoRedoPerformed -= Apply;
        }

        public void Init(RenderTexture texture)
        {
            m_Target = texture;
            m_Stack = new();
            m_Counter = CreateInstance<Counter>();

            Record();
        }

        public void Record()
        {
            for (var i = m_Counter.Count; i < m_Stack.Count; i++)
            {
                if (m_Stack[i] != null)
                {
                    DestroyImmediate(m_Stack[i]);
                }
            }
            m_Stack.RemoveRange(m_Counter.Count, m_Stack.Count - m_Counter.Count);

            // Set HideFlags to keep the state when reloading a scene or domain
            var texture = new Texture2D(m_Target.width, m_Target.height)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            RenderTexture.active = m_Target;
            texture.ReadPixels(new(0, 0, texture.width, texture.height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;

            m_Stack.Add(texture);

            if (m_Counter.Count > 0)
            {
                UnityEditor.Undo.RecordObject(m_Counter, "Modify Texture");
            }
            m_Counter.Count++;
        }

        public void Undo()
        {
            if (CanUndo)
            {
                UnityEditor.Undo.RecordObject(m_Counter, "Undo Texture");
                m_Counter.Count--;

                Apply();
            }
        }

        public void Redo()
        {
            if (CanRedo)
            {
                UnityEditor.Undo.RecordObject(m_Counter, "Redo Texture");
                m_Counter.Count++;

                Apply();
            }
        }

        public Texture2D Peek()
        {
            return m_Stack[m_Counter.Count - 1];
        }

#if MTE_NDMF
        public Texture2D ObservePeek(ComputeContext context)
        {
            return m_Stack[context.Observe(m_Counter, c => c.Count) - 1];
        }
#endif

        private void Apply()
        {
            Graphics.Blit(Peek(), m_Target);
            RenderTexture.active = null;
        }

        private void OnDestroy()
        {
            if (m_Stack != null)
            {
                foreach (var texture in m_Stack)
                {
                    if (texture != null)
                    {
                        DestroyImmediate(texture);
                    }
                }
                m_Stack = null!; // Reset
            }
            if (m_Counter != null)
            {
                DestroyImmediate(m_Counter);
                m_Counter = null!; // Reset
            }
        }
    }
}
