using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AntMediaServerUnityWebRTCClient))]
public class AntMediaServerUnityWebRTCClientEditor : Editor
{
    public override void OnInspectorGUI()
    {
        {
            var client = target as AntMediaServerUnityWebRTCClient;
            serializedObject.Update();
            var clientType = (AntMediaServerUnityWebRTCClient.ClientType)EditorGUILayout.EnumPopup("clientType", client.clientType);
            client.clientType = clientType;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("signalingUrl"), new GUIContent("Signaling URL"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("streamId"), new GUIContent("Stream ID"));
            ShowIceServerList(serializedObject.FindProperty("iceServers"));
            if(clientType == AntMediaServerUnityWebRTCClient.ClientType.Publisher)
            {
                //var videoStreamSource = (AntMediaServerUnityWebRTCClient.VideoStreamSource)EditorGUILayout.EnumPopup("videoStreamSource", client.videoStreamSource);
                //client.videoStreamSource = videoStreamSource;
                //if (videoStreamSource == AntMediaServerUnityWebRTCClient.VideoStreamSource.Camera)
                //    EditorGUILayout.PropertyField(serializedObject.FindProperty("camera"), new GUIContent("Stream Capture Camera"));
                //else
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("videoPlayer"), new GUIContent("Stream VideoPlayer"));
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("playerDisplay"), new GUIContent("Video Display"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("videoWidth"), new GUIContent("Video Width"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("videoHeight"), new GUIContent("Video Height"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dataChannelLabel"), new GUIContent("DataChannel Label"));
            if (clientType == AntMediaServerUnityWebRTCClient.ClientType.Publisher)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("videoBitrate"), new GUIContent("Video Bitrate (Kbps)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("logLevel"), new GUIContent("Log Level"));
            serializedObject.ApplyModifiedProperties();
        }
    }

    static void ShowIceServerList(SerializedProperty list)
    {
        EditorGUILayout.PropertyField(list.FindPropertyRelative("Array.size"), new GUIContent(list.displayName));
        EditorGUI.indentLevel += 1;
        for (int i = 0; i < list.arraySize; i++)
        {
            var element = list.GetArrayElementAtIndex(i);
            var label = "Ice server [" + i + "]";
            EditorGUILayout.PropertyField(element, new GUIContent(label), false);
            if (element.isExpanded)
            {
                EditorGUI.indentLevel += 1;
                EditorGUILayout.PropertyField(element.FindPropertyRelative("urls"), true);
                EditorGUILayout.PropertyField(element.FindPropertyRelative("username"));
                EditorGUILayout.PropertyField(element.FindPropertyRelative("credential"));
                EditorGUILayout.PropertyField(element.FindPropertyRelative("credentialType"));
                EditorGUI.indentLevel -= 1;
            }
        }
        EditorGUI.indentLevel -= 1;
    }
}
