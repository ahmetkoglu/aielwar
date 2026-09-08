using UnityEditor;
using UnityEngine;

public static class SetupWaveSpawner
{
    [MenuItem("Tools/Setup Wave Spawner")]
    public static void Execute()
    {
        WaveSpawner spawner = Object.FindObjectOfType<WaveSpawner>();
        if (spawner == null)
        {
            Debug.LogError("No WaveSpawner found in scene!");
            return;
        }

        GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cube Enemy Prototype.prefab");
        if (enemyPrefab == null)
        {
            Debug.LogError("Enemy prefab not found at Assets/Prefabs/Cube Enemy Prototype.prefab");
            return;
        }

        // Access the waves list via serialized object
        SerializedObject serializedObject = new SerializedObject(spawner);
        SerializedProperty wavesProperty = serializedObject.FindProperty("waves");

        // Clear existing waves
        wavesProperty.ClearArray();

        // Add Wave 1
        wavesProperty.InsertArrayElementAtIndex(0);
        SerializedProperty wave1 = wavesProperty.GetArrayElementAtIndex(0);
        wave1.FindPropertyRelative("waveName").stringValue = "Wave 1";
        wave1.FindPropertyRelative("enemyCount").intValue = 5;
        wave1.FindPropertyRelative("spawnInterval").floatValue = 1.5f;
        wave1.FindPropertyRelative("enemyPrefab").objectReferenceValue = enemyPrefab;

        // Add Wave 2
        wavesProperty.InsertArrayElementAtIndex(1);
        SerializedProperty wave2 = wavesProperty.GetArrayElementAtIndex(1);
        wave2.FindPropertyRelative("waveName").stringValue = "Wave 2";
        wave2.FindPropertyRelative("enemyCount").intValue = 8;
        wave2.FindPropertyRelative("spawnInterval").floatValue = 1.2f;
        wave2.FindPropertyRelative("enemyPrefab").objectReferenceValue = enemyPrefab;

        // Add Wave 3
        wavesProperty.InsertArrayElementAtIndex(2);
        SerializedProperty wave3 = wavesProperty.GetArrayElementAtIndex(2);
        wave3.FindPropertyRelative("waveName").stringValue = "Wave 3";
        wave3.FindPropertyRelative("enemyCount").intValue = 12;
        wave3.FindPropertyRelative("spawnInterval").floatValue = 1.0f;
        wave3.FindPropertyRelative("enemyPrefab").objectReferenceValue = enemyPrefab;

        serializedObject.ApplyModifiedProperties();

        // Set player target
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            SerializedProperty playerProp = serializedObject.FindProperty("playerTarget");
            playerProp.objectReferenceValue = player.transform;
            serializedObject.ApplyModifiedProperties();
        }

        Debug.Log("WaveSpawner setup complete with 3 waves!");
    }
}