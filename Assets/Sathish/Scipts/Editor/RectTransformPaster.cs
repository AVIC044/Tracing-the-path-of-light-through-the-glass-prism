using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class RectTransformPaster : MonoBehaviour
{
    public RectTransform reference;
    public List<RectTransform> targets = new List<RectTransform>();
}

[CustomEditor(typeof(RectTransformPaster))]
public class RectTransformPasterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        RectTransformPaster script = (RectTransformPaster)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Reference", EditorStyles.boldLabel);

        script.reference = (RectTransform)EditorGUILayout.ObjectField(
            "Reference Transform",
            script.reference,
            typeof(RectTransform),
            true
        );

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Targets", EditorStyles.boldLabel);

        int newSize = EditorGUILayout.IntField("Size", script.targets.Count);

        while (newSize > script.targets.Count)
            script.targets.Add(null);

        while (newSize < script.targets.Count)
            script.targets.RemoveAt(script.targets.Count - 1);

        for (int i = 0; i < script.targets.Count; i++)
        {
            script.targets[i] = (RectTransform)EditorGUILayout.ObjectField(
                $"Target {i}",
                script.targets[i],
                typeof(RectTransform),
                true
            );
        }

        EditorGUILayout.Space(10);

        GUI.backgroundColor = Color.green;

        if (GUILayout.Button("Paste Reference To All", GUILayout.Height(35)))
        {
            PasteReference(script);
        }

        GUI.backgroundColor = Color.white;

        EditorUtility.SetDirty(script);
    }

    private void PasteReference(RectTransformPaster script)
    {
        if (script.reference == null)
        {
            Debug.LogWarning("Please assign a Reference Transform.");
            return;
        }

        foreach (RectTransform target in script.targets)
        {
            if (target == null)
                continue;

            Undo.RecordObject(target, "Paste RectTransform");

            // Copy RectTransform properties
            target.anchorMin = script.reference.anchorMin;
            target.anchorMax = script.reference.anchorMax;
            target.pivot = script.reference.pivot;
            target.anchoredPosition = script.reference.anchoredPosition;
            target.sizeDelta = script.reference.sizeDelta;
            target.localRotation = script.reference.localRotation;
            target.localScale = script.reference.localScale;
        }

        Debug.Log("Reference RectTransform pasted to all targets.");
    }
}