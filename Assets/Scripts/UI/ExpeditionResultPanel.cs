using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// M1용 임시 영지/결과 화면. 영지에서는 TickManager를 만들지 않아 시간이 흐르지 않는다.
public sealed class ExpeditionResultPanel : MonoBehaviour
{
    private RectTransform panelRect;
    private Button startButton;
    private Text startLabel;

    private void Start()
    {
        Build();
        LootRuntimeState.Changed += RefreshStartButton;
        if (GetComponent<BaseCampManagementPanel>() == null) gameObject.AddComponent<BaseCampManagementPanel>();
        if (GetComponent<BaseCampGridPanel>() == null) gameObject.AddComponent<BaseCampGridPanel>();
    }

    private void OnDestroy() => LootRuntimeState.Changed -= RefreshStartButton;

    private void Build()
    {
        Transform root = ScreenSpaceCanvasProvider.GetOrCreateHudRoot();

        var panel = CreateUiObject("M1ResultPanel", root);
        var panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.04f, 0.05f, 0.08f, 0.96f);
        panelRect = panel.GetComponent<RectTransform>();
        ApplyLayout();

        string body;
        var result = ExpeditionSession.LastResult;
        if (result == null)
        {
            body = "원정 준비\n\n1층부터 3층까지 내려가 탈출하세요.";
        }
        else
        {
            string resultName = result.Result == ExpeditionResult.Escaped ? "생환"
                : result.Result == ExpeditionResult.Dead ? "사망" : "MIA";
            body = $"원정 결과: {resultName}\n\n경과 Tick: {result.ElapsedTicks:0}\n도달 층: {result.ReachedFloor}층";
        }

        string recoveryNotice = M5SaveService.ConsumeRecoveryNotice();
        if (!string.IsNullOrEmpty(recoveryNotice)) body = recoveryNotice + "\n\n" + body;

        var textGo = CreateUiObject("ResultText", panel.transform);
        var text = textGo.AddComponent<Text>();
        text.font = HudFont.GetDefault();
        text.fontSize = 25;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = body;
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.08f, 0.25f);
        textRect.anchorMax = new Vector2(0.92f, 0.92f);
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;

        var buttonGo = CreateUiObject("StartExpeditionButton", panel.transform);
        var buttonImage = buttonGo.AddComponent<Image>();
        buttonImage.color = new Color(0.18f, 0.48f, 0.28f, 1f);
        startButton = buttonGo.AddComponent<Button>();
        startButton.targetGraphic = buttonImage;
        startButton.onClick.AddListener(StartExpedition);
        var buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.3f, 0.08f);
        buttonRect.anchorMax = new Vector2(0.7f, 0.22f);
        buttonRect.offsetMin = buttonRect.offsetMax = Vector2.zero;

        var labelGo = CreateUiObject("Label", buttonGo.transform);
        startLabel = labelGo.AddComponent<Text>();
        startLabel.font = HudFont.GetDefault();
        startLabel.fontSize = 28;
        startLabel.alignment = TextAnchor.MiddleCenter;
        startLabel.color = Color.white;
        startLabel.text = result == null ? "출격" : "다시 출격";
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        RefreshStartButton();
    }

    private void Update() => ApplyLayout();

    private void ApplyLayout()
    {
        if (panelRect != null) BaseCampLayout.ApplyTo(panelRect, BaseCampLayout.Calculate().Expedition);
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StartExpedition()
    {
        if (!LootRuntimeState.CanStartExpedition) return;
        M5SaveService.PrepareForExpedition();
        ExpeditionSession.BeginNewRun();
        SceneManager.LoadScene("InDungeon");
    }

    private void RefreshStartButton()
    {
        if (startButton == null || startLabel == null) return;
        startButton.interactable = LootRuntimeState.CanStartExpedition;
        startLabel.text = LootRuntimeState.CanStartExpedition
            ? (ExpeditionSession.LastResult == null ? "출격" : "다시 출격")
            : "임시 보관함을 정리하세요";
        startLabel.fontSize = LootRuntimeState.CanStartExpedition ? 28 : 15;
    }
}
