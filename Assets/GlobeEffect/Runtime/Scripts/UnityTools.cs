using UnityEngine;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Kleine Unity-Werkzeuge, die mehrere Skripte gleich brauchen.
    /// </summary>
    public static class UnityTools
    {
        /// <summary>
        /// Baut ein Material mit dem passenden Shader. Das Material wird nicht
        /// gespeichert und ist nach dem Schließen wieder weg. Wird der Shader nicht
        /// gefunden, kommt eine Fehlermeldung und null zurück.
        /// </summary>
        public static Material CreateShaderMaterial(
            string resourceName, string fallbackName, string materialName, Object logContext)
        {
            // Erst im Resources-Ordner suchen, dann über den Shader-Namen.
            Shader shader = Resources.Load<Shader>(resourceName);
            if (shader == null)
            {
                shader = Shader.Find(fallbackName);
            }

            if (shader == null)
            {
                Debug.LogError($"Shader '{fallbackName}' wurde nicht gefunden.", logContext);
                return null;
            }

            return new Material(shader) { name = materialName, hideFlags = HideFlags.HideAndDontSave };
        }

        /// <summary>
        /// Hängt ein neues Objekt mit Mesh und Material unter parent. Schatten
        /// braucht hier nichts, und gespeichert wird das Objekt auch nicht.
        /// </summary>
        public static MeshRenderer CreateMeshRenderer(
            Transform parent, string name, Mesh mesh, Material material, int sortingOrder)
        {
            var child = new GameObject(name) { hideFlags = HideFlags.DontSave };
            child.transform.SetParent(parent, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        public static void DeleteObject(Object objectToDelete)
        {
            if (objectToDelete == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                // Im Play Mode räumt Unity das am Ende vom Frame weg.
                Object.Destroy(objectToDelete);
            }
            else
            {
                // Außerhalb vom Play Mode sofort, damit die Vorschau gleich stimmt.
                Object.DestroyImmediate(objectToDelete);
            }
        }

        // Wenn man eine Komponente neu ans Objekt hängt, wird gleich die Main
        // Camera eingetragen. In VR ist das die Kamera im Headset.
        public static Transform MainCameraTransform()
        {
            Camera mainCamera = Camera.main;
            return mainCamera != null ? mainCamera.transform : null;
        }

        /// <summary>
        /// Gibt das eingetragene Objekt zurück. Ist das Feld im Inspector leer,
        /// wird das passende Objekt in der Szene gesucht.
        ///
        /// Bewusst mit != null statt ??, weil nur Unitys eigener Vergleich auch
        /// gelöschte oder fehlende Objekte als leer erkennt.
        /// </summary>
        public static T FindIfMissing<T>(T current) where T : Object
        {
            return current != null ? current : Object.FindAnyObjectByType<T>();
        }
    }
}
