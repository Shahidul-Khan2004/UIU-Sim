#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UIU.Simulator.Gameplay.Editor
{
    /// <summary>
    /// Editor utility to place and configure the Canteen Middle Breakfast Counter
    /// and Two Side Stores in the GroundFloor scene.
    /// </summary>
    public static class CanteenSceneSetup
    {
        private const string GroundFloorScenePath = "Assets/Scenes/Floors/GroundFloor.unity";
        private const string SuccessSoundPath = "Assets/Audio/Interactions/IDScanner/success.wav";
        private const string FailureSoundPath = "Assets/Audio/Interactions/IDScanner/failure.wav";
        private const string BellSoundPath = "Assets/Casual Game Sounds U6/CasualGameSounds/DM-CGS-21.wav";

        [MenuItem("UIU Simulator/Setup/Setup Canteen Counters")]
        public static void SetupCanteenCounters()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            bool openedScene = false;

            if (activeScene.path != GroundFloorScenePath)
            {
                activeScene = EditorSceneManager.OpenScene(GroundFloorScenePath, OpenSceneMode.Single);
                openedScene = true;
            }

            GameObject interactablesRoot = GameObject.Find("Interactables");
            if (interactablesRoot == null)
            {
                interactablesRoot = new GameObject("Interactables");
                Undo.RegisterCreatedObjectUndo(interactablesRoot, "Create Interactables Root");
            }

            AudioClip successClip = AssetDatabase.LoadAssetAtPath<AudioClip>(SuccessSoundPath);
            AudioClip failureClip = AssetDatabase.LoadAssetAtPath<AudioClip>(FailureSoundPath);
            AudioClip bellClip = AssetDatabase.LoadAssetAtPath<AudioClip>(BellSoundPath);
            if (bellClip == null) bellClip = successClip;

            // 1. Middle Breakfast Counter
            SetupMiddleCounter(interactablesRoot.transform, bellClip, failureClip);

            // 2. Left Side Store
            SetupSideStore(interactablesRoot.transform, "Canteen_SideStore_Left", "Left Canteen Store", new Vector3(-41f, 0f, 70f), successClip);

            // 3. Right Side Store
            SetupSideStore(interactablesRoot.transform, "Canteen_SideStore_Right", "Right Canteen Store", new Vector3(-29f, 0f, 70f), successClip);

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("<color=green><b>[CanteenSceneSetup] Canteen counters successfully set up and saved in GroundFloor scene!</b></color>");
        }

        private static void SetupMiddleCounter(Transform parent, AudioClip successSound, AudioClip failureSound)
        {
            Transform existing = parent.Find("Canteen_MiddleBreakfastCounter");
            GameObject counterGo;

            if (existing != null)
            {
                counterGo = existing.gameObject;
            }
            else
            {
                counterGo = new GameObject("Canteen_MiddleBreakfastCounter");
                counterGo.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(counterGo, "Create Middle Breakfast Counter");
            }

            counterGo.transform.position = new Vector3(-35f, 0f, 71f);
            counterGo.transform.rotation = Quaternion.identity;

            // BoxCollider
            BoxCollider col = counterGo.GetComponent<BoxCollider>();
            if (col == null) col = counterGo.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.size = new Vector3(3.2f, 1.2f, 1.2f);
            col.isTrigger = false;

            // AudioSource
            AudioSource audio = counterGo.GetComponent<AudioSource>();
            if (audio == null) audio = counterGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            // InteractionFeedback
            InteractionFeedback feedback = counterGo.GetComponent<InteractionFeedback>();
            if (feedback == null) feedback = counterGo.AddComponent<InteractionFeedback>();

            SerializedObject serializedFeedback = new SerializedObject(feedback);
            serializedFeedback.FindProperty("successSound").objectReferenceValue = successSound;
            serializedFeedback.FindProperty("failureSound").objectReferenceValue = failureSound;
            serializedFeedback.ApplyModifiedProperties();

            // CanteenBreakfastCounter
            CanteenBreakfastCounter counter = counterGo.GetComponent<CanteenBreakfastCounter>();
            if (counter == null) counter = counterGo.AddComponent<CanteenBreakfastCounter>();

            SerializedObject serializedCounter = new SerializedObject(counter);
            serializedCounter.FindProperty("workerName").stringValue = "Canteen Worker";
            serializedCounter.FindProperty("prompt").stringValue = "Order Breakfast";
            serializedCounter.FindProperty("queueDuration").floatValue = 75f;
            serializedCounter.ApplyModifiedProperties();

            // Visual mesh
            SetupVisualCounterModel(counterGo.transform, "CounterModel", new Vector3(3f, 1f, 1f));
        }

        private static void SetupSideStore(Transform parent, string objectName, string storeName, Vector3 position, AudioClip successSound)
        {
            Transform existing = parent.Find(objectName);
            GameObject storeGo;

            if (existing != null)
            {
                storeGo = existing.gameObject;
            }
            else
            {
                storeGo = new GameObject(objectName);
                storeGo.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(storeGo, "Create Side Store");
            }

            storeGo.transform.position = position;
            storeGo.transform.rotation = Quaternion.identity;

            // BoxCollider
            BoxCollider col = storeGo.GetComponent<BoxCollider>();
            if (col == null) col = storeGo.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.size = new Vector3(2.5f, 1.2f, 1.2f);
            col.isTrigger = false;

            // AudioSource
            AudioSource audio = storeGo.GetComponent<AudioSource>();
            if (audio == null) audio = storeGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            // InteractionFeedback
            InteractionFeedback feedback = storeGo.GetComponent<InteractionFeedback>();
            if (feedback == null) feedback = storeGo.AddComponent<InteractionFeedback>();

            SerializedObject serializedFeedback = new SerializedObject(feedback);
            serializedFeedback.FindProperty("successSound").objectReferenceValue = successSound;
            serializedFeedback.ApplyModifiedProperties();

            // CanteenSideStore
            CanteenSideStore store = storeGo.GetComponent<CanteenSideStore>();
            if (store == null) store = storeGo.AddComponent<CanteenSideStore>();

            SerializedObject serializedStore = new SerializedObject(store);
            serializedStore.FindProperty("storeName").stringValue = storeName;
            serializedStore.FindProperty("prompt").stringValue = "Order Food";
            serializedStore.FindProperty("greeting").stringValue = $"Welcome to the {storeName}! What can I get for you?";
            serializedStore.ApplyModifiedProperties();

            // Visual mesh
            SetupVisualCounterModel(storeGo.transform, "StoreModel", new Vector3(2.4f, 1f, 1f));
        }

        private static void SetupVisualCounterModel(Transform parent, string name, Vector3 scale)
        {
            Transform modelTransform = parent.Find(name);
            GameObject modelGo;

            if (modelTransform == null)
            {
                modelGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                modelGo.name = name;
                modelGo.transform.SetParent(parent, false);
                // Remove primitive collider as parent has BoxCollider
                Collider primitiveCol = modelGo.GetComponent<Collider>();
                if (primitiveCol != null) Object.DestroyImmediate(primitiveCol);
            }
            else
            {
                modelGo = modelTransform.gameObject;
            }

            modelGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            modelGo.transform.localRotation = Quaternion.identity;
            modelGo.transform.localScale = scale;
        }

        public static void SetupCanteenFromCommandLine()
        {
            SetupCanteenCounters();
        }
    }
}
#endif
