using UnityEngine;
using UnityEditor;

public static class EnemyAudioSetup
{
    [MenuItem("Tools/Setup Enemy Fire Audio")]
    public static void Execute()
    {
        AudioClip audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Weapons/rifle_fire_assault_01.wav");
        if (audioClip == null)
        {
            Debug.LogError("Audio clip not found at Assets/Audio/Weapons/rifle_fire_assault_01.wav");
            return;
        }

        EnemyChaseAttack[] enemies = Object.FindObjectsOfType<EnemyChaseAttack>();
        int count = 0;

        foreach (EnemyChaseAttack enemy in enemies)
        {
            AudioSource audioSource = enemy.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = enemy.gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 2.5f;
            audioSource.maxDistance = 45f;
            audioSource.dopplerLevel = 0.15f;

            SerializedObject so = new SerializedObject(enemy);
            so.FindProperty("fireAudioSource").objectReferenceValue = audioSource;
            so.FindProperty("fireClip").objectReferenceValue = audioClip;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(enemy);
            count++;
            Debug.Log($"Set fire audio on {enemy.name}");
        }

        Debug.Log($"Enemy fire audio setup complete. Updated {count} enemies.");
    }
}