#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class LaunchSequenceLayoutPersistence
{
    private static Font Galmuri11Font => AssetDatabase.LoadAssetAtPath<Font>(
        "Assets/UI/Galmuri11-Condensed.ttf");
    private static bool hasCachedLayout;
    private static Vector2 operatorPosition;
    private static Vector2 operatorSize;
    private static float operatorPortraitZoom;
    private static Vector2 operatorPortraitFocus;
    private static Vector2 radarPosition;
    private static Vector2 radarSize;

    static LaunchSequenceLayoutPersistence()
    {
        // Intentionally empty. Layout values must only change through explicit user interaction.
    }

    private static void CacheLayoutWhilePlaying()
    {
        if (!EditorApplication.isPlaying)
        {
            EnsureLayoutHierarchyInOpenScene();
            return;
        }

        LaunchSequenceController controller = Object.FindFirstObjectByType<LaunchSequenceController>();
        if (controller == null)
            return;

        CacheLayout(controller);
    }

    private static void EnsureLayoutHierarchyInOpenScene()
    {
        LaunchSequenceController[] controllers = Object.FindObjectsByType<LaunchSequenceController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (LaunchSequenceController controller in controllers)
        {
            if (!controller.gameObject.scene.IsValid() || !controller.EnsureLayoutHierarchy())
                continue;

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            SceneView.RepaintAll();
        }
    }

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Active)]
    private static void DrawLayoutLabels(LaunchSequenceController controller, GizmoType gizmoType)
    {
        Transform root = controller.transform.Find("Launch Scene Layout (MOVE THESE)");
        if (root == null)
            return;

        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            font = Galmuri11Font,
            normal = { textColor = new Color(1f, 0.88f, 0.2f) },
            fontSize = 13
        };

        foreach (Transform point in root)
        {
            Handles.Label(point.position + Vector3.up * 0.18f, point.name, labelStyle);
        }

        if (controller.TextSettings != null)
        {
            LaunchSequenceTextSettings text = controller.TextSettings;
            Vector3 preview = text.transform.position + new Vector3(0.25f, 2.2f, 0f);
            GUIStyle previewStyle = new GUIStyle(EditorStyles.helpBox)
            {
                font = Galmuri11Font,
                normal = { textColor = Color.white },
                fontSize = 12
            };
            Handles.Label(preview,
                $"TEXT PREVIEW\n{text.radarTitle}\n{text.detectionLegend}\n{text.lockOnLegend}\n{text.pressToLaunch}\n{text.maximumThrust}",
                previewStyle);

            if (controller.MissileWarningTextHandle != null)
            {
                GUIStyle warningStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    font = Galmuri11Font,
                    normal = { textColor = new Color(1f, 0.12f, 0.06f) },
                    fontSize = 19
                };
                Handles.Label(controller.MissileWarningTextHandle.position,
                    text.missileWarning, warningStyle);
            }
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            hasCachedLayout = false;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            LaunchSequenceController controller = Object.FindFirstObjectByType<LaunchSequenceController>();
            if (controller != null)
                CacheLayout(controller);
        }
        else if (state == PlayModeStateChange.EnteredEditMode && hasCachedLayout)
        {
            EditorApplication.delayCall += ApplyCachedLayout;
        }
    }

    private static void CacheLayout(LaunchSequenceController controller)
    {
        operatorPosition = controller.OperatorPanelPosition;
        operatorSize = controller.OperatorPanelSize;
        operatorPortraitZoom = controller.OperatorPortraitZoom;
        operatorPortraitFocus = controller.OperatorPortraitFocus;
        radarPosition = controller.RadarPanelPosition;
        radarSize = controller.RadarPanelSize;
        hasCachedLayout = true;
    }

    private static void ApplyCachedLayout()
    {
        LaunchSequenceController controller = Object.FindFirstObjectByType<LaunchSequenceController>();
        if (controller == null)
            return;

        Undo.RecordObject(controller, "Apply Launch Sequence Panel Layout");
        controller.OperatorPanelPosition = operatorPosition;
        controller.OperatorPanelSize = operatorSize;
        controller.OperatorPortraitZoom = operatorPortraitZoom;
        controller.OperatorPortraitFocus = operatorPortraitFocus;
        controller.RadarPanelPosition = radarPosition;
        controller.RadarPanelSize = radarSize;
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        hasCachedLayout = false;
    }
}

[CustomEditor(typeof(LaunchSequenceController))]
public sealed class LaunchSequenceControllerEditor : Editor
{
    private static Font Galmuri11Font => AssetDatabase.LoadAssetAtPath<Font>(
        "Assets/UI/Galmuri11-Condensed.ttf");
    private enum DragTarget
    {
        None,
        OperatorMove,
        OperatorResize,
        RadarMove,
        RadarResize
    }

    private static DragTarget dragTarget;
    private static Vector2 dragStartMouse;
    private static Vector2 dragStartPosition;
    private static Vector2 dragStartSize;

    public static void DrawSceneLayout(LaunchSequenceController controller, SceneView sceneView)
    {
        if (controller == null || sceneView == null)
            return;

        float viewWidth = Mathf.Max(1f, sceneView.position.width);
        float viewHeight = Mathf.Max(1f, sceneView.position.height - 22f);
        Rect operatorRect = ToSceneRect(controller.OperatorPanelPosition,
            controller.OperatorPanelSize, viewWidth, viewHeight);
        Rect radarRect = ToSceneRect(controller.RadarPanelPosition,
            controller.RadarPanelSize, viewWidth, viewHeight);

        Handles.BeginGUI();
        DrawInstructions();
        DrawOperatorPreview(controller, operatorRect);
        DrawRadarPreview(controller, radarRect);
        HandlePanelInput(controller, operatorRect, radarRect, viewWidth, viewHeight);
        Handles.EndGUI();
    }

    private static Rect ToSceneRect(Vector2 position, Vector2 size, float width, float height)
    {
        return new Rect(position.x * width, position.y * height, size.x * width, size.y * height);
    }

    private static void DrawInstructions()
    {
        GUIStyle style = new GUIStyle(EditorStyles.helpBox)
        {
            font = Galmuri11Font,
            fontSize = 13,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.9f, 0.25f) }
        };
        GUI.Label(new Rect(10f, 8f, 500f, 24f),
            "LAUNCH UI LAYOUT  |  DRAG PANEL: MOVE  |  YELLOW CORNER: RESIZE", style);
    }

    private static void DrawOperatorPreview(LaunchSequenceController controller, Rect rect)
    {
        DrawPanel(rect, new Color(0.015f, 0.09f, 0.14f, 0.9f), new Color(0.08f, 0.9f, 1f));
        GUIStyle title = PanelStyle(13, FontStyle.Bold, new Color(0.4f, 0.95f, 1f), TextAnchor.UpperLeft);
        GUI.Label(new Rect(rect.x + 10f, rect.y + 7f, rect.width - 20f, 22f), "OPERATOR PANEL  //  CARRIER COMM // IRIS", title);

        Rect portraitRect = new Rect(rect.x + 10f, rect.y + 34f,
            rect.width * 0.53f, Mathf.Max(20f, rect.height - 44f));
        EditorGUI.DrawRect(portraitRect, new Color(0.02f, 0.12f, 0.17f, 1f));
        if (controller.OperatorSprite != null && controller.OperatorSprite.texture != null)
        {
            Sprite sprite = controller.OperatorSprite;
            Texture2D texture = sprite.texture;
            Rect spriteRect = sprite.rect;
            Rect uv = new Rect(spriteRect.x / texture.width, spriteRect.y / texture.height,
                spriteRect.width / texture.width, spriteRect.height / texture.height);
            GUI.DrawTextureWithTexCoords(portraitRect, texture, uv, true);
        }
        else
        {
            GUIStyle portrait = PanelStyle(12, FontStyle.Bold,
                new Color(0.45f, 0.7f, 0.78f), TextAnchor.MiddleCenter);
            GUI.Label(portraitRect, "OPERATOR PORTRAIT", portrait);
        }

        string message = controller.TextSettings != null
            ? LaunchSequenceController.NormalizeDisplayText(controller.TextSettings.radioCheck)
            : "VIPER 01, IRIS.\n\nRADIO CHECK.";
        GUIStyle body = PanelStyle(12, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        body.wordWrap = true;
        GUI.Label(new Rect(rect.x + rect.width * 0.58f, rect.y + 34f,
            rect.width * 0.39f, Mathf.Max(20f, rect.height - 44f)), message, body);
        DrawResizeHandle(rect);
    }

    private static void DrawRadarPreview(LaunchSequenceController controller, Rect rect)
    {
        DrawPanel(rect, new Color(0.01f, 0.035f, 0.055f, 0.82f), new Color(0.2f, 0.75f, 0.85f));
        LaunchSequenceTextSettings text = controller.TextSettings;
        GUIStyle title = PanelStyle(13, FontStyle.Bold, new Color(0.4f, 0.95f, 1f), TextAnchor.MiddleCenter);
        GUI.Label(new Rect(rect.x + 8f, rect.y + 7f, rect.width - 16f, 24f),
            text != null ? text.radarTitle : "UF-35 RADAR / LOCK-ON TUTORIAL", title);

        GUIStyle left = PanelStyle(12, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        GUI.Label(new Rect(rect.x + 18f, rect.y + 43f, rect.width - 36f, 22f),
            text != null ? text.detectionLegend : "WHITE  90°  DETECTION", left);
        left.normal.textColor = new Color(0.45f, 1f, 0.4f);
        GUI.Label(new Rect(rect.x + 18f, rect.y + 68f, rect.width - 36f, 22f),
            text != null ? text.lockOnLegend : "GREEN  60°  LOCK-ON", left);

        GUIStyle bottom = PanelStyle(11, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        bottom.wordWrap = true;
        GUI.Label(new Rect(rect.x + 12f, rect.yMax - 48f, rect.width - 24f, 38f),
            text != null ? text.radarDescription : "TARGET ENTERS 90°: DETECT / TARGET ENTERS 60°: LOCK ON", bottom);
        DrawResizeHandle(rect);
    }

    private static void HandlePanelInput(LaunchSequenceController controller, Rect operatorRect,
        Rect radarRect, float viewWidth, float viewHeight)
    {
        Event evt = Event.current;
        Rect operatorResize = ResizeRect(operatorRect);
        Rect radarResize = ResizeRect(radarRect);

        if (evt.type == EventType.MouseDown && evt.button == 0)
        {
            if (operatorResize.Contains(evt.mousePosition))
                BeginDrag(controller, DragTarget.OperatorResize, controller.OperatorPanelPosition, controller.OperatorPanelSize, evt);
            else if (radarResize.Contains(evt.mousePosition))
                BeginDrag(controller, DragTarget.RadarResize, controller.RadarPanelPosition, controller.RadarPanelSize, evt);
            else if (operatorRect.Contains(evt.mousePosition))
                BeginDrag(controller, DragTarget.OperatorMove, controller.OperatorPanelPosition, controller.OperatorPanelSize, evt);
            else if (radarRect.Contains(evt.mousePosition))
                BeginDrag(controller, DragTarget.RadarMove, controller.RadarPanelPosition, controller.RadarPanelSize, evt);
        }
        else if (evt.type == EventType.MouseDrag && evt.button == 0 && dragTarget != DragTarget.None)
        {
            Vector2 delta = evt.mousePosition - dragStartMouse;
            Vector2 normalizedDelta = new Vector2(delta.x / viewWidth, delta.y / viewHeight);
            ApplyDrag(controller, normalizedDelta);
            EditorUtility.SetDirty(controller);
            SceneView.RepaintAll();
            evt.Use();
        }
        else if (evt.type == EventType.MouseUp && evt.button == 0 && dragTarget != DragTarget.None)
        {
            dragTarget = DragTarget.None;
            evt.Use();
        }
    }

    private static void BeginDrag(LaunchSequenceController controller, DragTarget nextTarget,
        Vector2 position, Vector2 size, Event evt)
    {
        Undo.RecordObject(controller, "Edit Launch Sequence UI Layout");
        dragTarget = nextTarget;
        dragStartMouse = evt.mousePosition;
        dragStartPosition = position;
        dragStartSize = size;
        evt.Use();
    }

    private static void ApplyDrag(LaunchSequenceController controller, Vector2 delta)
    {
        bool isOperator = dragTarget == DragTarget.OperatorMove || dragTarget == DragTarget.OperatorResize;
        bool isResize = dragTarget == DragTarget.OperatorResize || dragTarget == DragTarget.RadarResize;

        if (isResize)
        {
            Vector2 size = dragStartSize + delta;
            size.x = Mathf.Clamp(size.x, 0.15f, 1f - dragStartPosition.x);
            size.y = Mathf.Clamp(size.y, 0.15f, 1f - dragStartPosition.y);
            if (isOperator)
                controller.OperatorPanelSize = size;
            else
                controller.RadarPanelSize = size;
        }
        else
        {
            Vector2 position = dragStartPosition + delta;
            position.x = Mathf.Clamp(position.x, 0f, 1f - dragStartSize.x);
            position.y = Mathf.Clamp(position.y, 0f, 1f - dragStartSize.y);
            if (isOperator)
                controller.OperatorPanelPosition = position;
            else
                controller.RadarPanelPosition = position;
        }
    }

    private static void DrawPanel(Rect rect, Color fill, Color border)
    {
        EditorGUI.DrawRect(rect, fill);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), border);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), border);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), border);
        EditorGUI.DrawRect(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), border);
    }

    private static void DrawResizeHandle(Rect rect)
    {
        EditorGUI.DrawRect(ResizeRect(rect), new Color(1f, 0.78f, 0.05f));
    }

    private static Rect ResizeRect(Rect rect)
    {
        const float size = 20f;
        return new Rect(rect.xMax - size, rect.yMax - size, size, size);
    }

    private static GUIStyle PanelStyle(int fontSize, FontStyle fontStyle, Color color, TextAnchor alignment)
    {
        return new GUIStyle(EditorStyles.label)
        {
            font = Galmuri11Font,
            fontSize = fontSize + 1,
            fontStyle = fontStyle,
            alignment = alignment,
            clipping = TextClipping.Clip,
            normal = { textColor = color }
        };
    }
}

[InitializeOnLoad]
public static class LaunchSequenceSceneLayoutOverlay
{
    static LaunchSequenceSceneLayoutOverlay()
    {
        SceneView.duringSceneGui -= Draw;
        SceneView.duringSceneGui += Draw;
    }

    private static void Draw(SceneView sceneView)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        LaunchSequenceController controller = Object.FindFirstObjectByType<LaunchSequenceController>();
        if (controller != null && controller.gameObject.scene.IsValid())
            LaunchSequenceControllerEditor.DrawSceneLayout(controller, sceneView);
    }
}
#endif
