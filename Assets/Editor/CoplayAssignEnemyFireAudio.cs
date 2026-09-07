using UnityEditor;
using UnityEngine;

public static class CoplayAssignEnemyFireAudio
{
    public static void Execute()
    {
        AudioClip fireClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Weapons/rifle_fire_assault_01.wav");
        if (fireClip == null)
        {
            Debug.LogError("CoplayAssignEnemyFireAudio: Could not load enemy fire clip.");
            return;
        }

        EnemyChaseAttack[] enemies = Object.FindObjectsByType<EnemyChaseAttack>(FindObjectsSortMode.None);
        int updated = 0;

        foreach (EnemyChaseAttack enemy in enemies)
        {
            if (enemy == null || !enemy.name.Contains("Cube Enemy Prototype"))
            {
                continue;
            }

            AudioSource audioSource = enemy.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = Undo.AddComponent<AudioSource>(enemy.gameObject);
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 2.5f;
            audioSource.maxDistance = 45f;
            audioSource.dopplerLevel = 0.15f;

            SerializedObject serializedEnemy = new SerializedObject(enemy);
            serializedEnemy.FindProperty("fireAudioSource").objectReferenceValue = audioSource;
            serializedEnemy.FindProperty("fireClip").objectReferenceValue = fireClip;
            serializedEnemy.FindProperty("fireVolume").floatValue = 0.75f;
            serializedEnemy.FindProperty("firePitchRandomness").floatValue = 0.04f;
            serializedEnemy.ApplyModifiedProperties();

            EditorUtility.SetDirty(enemy);
            EditorUtility.SetDirty(audioSource);
            updated++;
        }

        Debug.Log($"CoplayAssignEnemyFireAudio: Assigned 3D enemy fire audio to {updated} cube enemies.");
    }
}