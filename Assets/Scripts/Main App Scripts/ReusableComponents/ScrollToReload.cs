using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[RequireComponent(typeof(ScrollRect))]
public class ScrollToReload : MonoBehaviour
{
    [Header("UI References")]
    public ScrollRect scrollRect;
    public RectTransform contentTransform;

    [Header("Reload Indicator")]
    public GameObject reloadIndicator;
    public TextMeshProUGUI reloadText;
    public Image reloadIcon;

    [Header("Settings")]
    public float pullThreshold = 150f;
    public float releaseThreshold = 100f;
    public float snapSpeed = 10f;
    public bool enableHapticFeedback = true;

    [Header("Messages")]
    public string pullMessage = "Pull down to refresh";
    public string releaseMessage = "Release to refresh";
    public string loadingMessage = "Loading...";

    [Header("Events")]
    public UnityEvent OnReloadTriggered;

    // Private variables
    private bool isRefreshing = false;
    private bool isPulling = false;
    private bool hasTriggeredReload = false;
    private Vector2 lastTouchPosition;
    private float currentPullDistance = 0f;
    private Vector3 originalIndicatorPosition;
    private RectTransform indicatorRect;
    private bool wasPressed = false;

    void Start()
    {
        if (scrollRect == null)
            scrollRect = GetComponent<ScrollRect>();

        if (contentTransform == null)
            contentTransform = scrollRect.content;

        if (reloadIndicator != null)
        {
            indicatorRect = reloadIndicator.GetComponent<RectTransform>();
            originalIndicatorPosition = indicatorRect.localPosition;
            reloadIndicator.SetActive(false);
        }

        scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
    }

    void Update()
    {
        if (isRefreshing) return;

        if (Pointer.current != null)
        {
            bool isPressed = Pointer.current.press.isPressed;

            if (isPressed != wasPressed)
            {
                if (isPressed)
                {
                    OnTouchStart(Pointer.current.position.ReadValue());
                }
                else
                {
                    OnTouchEnd();
                }
                wasPressed = isPressed;
            }
            else if (isPressed && isPulling)
            {
                OnTouchMove(Pointer.current.position.ReadValue());
            }
        }
        if (isPulling)
        {
            UpdateIndicatorPosition();
        }
    }

    void OnTouchStart(Vector2 position)
    {
        lastTouchPosition = position;

        if (scrollRect.verticalNormalizedPosition >= 0.99f)
        {
            isPulling = true;
            hasTriggeredReload = false;
        }
    }

    void OnTouchMove(Vector2 position)
    {
        if (!isPulling) return;

        float deltaY = position.y - lastTouchPosition.y;

        if (deltaY < 0 && scrollRect.verticalNormalizedPosition >= 0.99f)
        {
            currentPullDistance += Mathf.Abs(deltaY) * 0.5f;
            currentPullDistance = Mathf.Max(0, currentPullDistance);

            // Show indicator when pull threshold is reached
            if (currentPullDistance > releaseThreshold && reloadIndicator != null && !reloadIndicator.activeInHierarchy)
            {
                reloadIndicator.SetActive(true);
                if (enableHapticFeedback)
                {
#if UNITY_ANDROID || UNITY_IOS
                    Handheld.Vibrate();
#endif
                }
            }

            UpdateReloadMessage();

            if (currentPullDistance > pullThreshold && !hasTriggeredReload)
            {
                hasTriggeredReload = true;
                if (enableHapticFeedback)
                {
#if UNITY_ANDROID || UNITY_IOS
                    Handheld.Vibrate();
#endif
                }
            }
        }
        lastTouchPosition = position;
    }

    void OnTouchEnd()
    {
        if (!isPulling) return;

        isPulling = false;
        if (hasTriggeredReload && currentPullDistance > pullThreshold)
        {
            StartReload();
        }
        else
        {
            StartCoroutine(SnapBackIndicator());
        }
    }

    void OnScrollValueChanged(Vector2 value)
    {
        if (scrollRect.verticalNormalizedPosition < 0.99f && isPulling)
        {
            isPulling = false;
            StartCoroutine(SnapBackIndicator());
        }
    }

    void UpdateIndicatorPosition()
    {
        if (reloadIndicator == null || indicatorRect == null) return;

        if (currentPullDistance > releaseThreshold)
        {
            float indicatorOffset = Mathf.Min(currentPullDistance - releaseThreshold, 100f);
            Vector3 newPosition = originalIndicatorPosition;
            newPosition.y = originalIndicatorPosition.y - indicatorOffset;
            indicatorRect.localPosition = newPosition;
        }
    }

    void UpdateReloadMessage()
    {
        if (reloadText == null) return;

        if (currentPullDistance > pullThreshold)
        {
            reloadText.text = releaseMessage;
        }
        else
        {
            reloadText.text = pullMessage;
        }
    }

    void StartReload()
    {
        if (isRefreshing) return;

        isRefreshing = true;

        if (reloadText != null)
            reloadText.text = loadingMessage;

        if (reloadIcon != null)
            StartCoroutine(RotateReloadIcon());

        OnReloadTriggered?.Invoke();

        StartCoroutine(AutoCompleteReload());
    }

    IEnumerator RotateReloadIcon()
    {
        if (reloadIcon == null) yield break;

        while (isRefreshing)
        {
            reloadIcon.transform.Rotate(0, 0, -360 * Time.deltaTime);
            yield return null;
        }

        reloadIcon.transform.rotation = Quaternion.identity;
    }

    IEnumerator AutoCompleteReload()
    {
        yield return new WaitForSeconds(3f);

        if (isRefreshing)
        {
            CompleteReload();
        }
    }

    IEnumerator SnapBackIndicator()
    {
        if (reloadIndicator == null) yield break;
        while (currentPullDistance > 0)
        {
            currentPullDistance = Mathf.Lerp(currentPullDistance, 0, snapSpeed * Time.deltaTime);

            if (currentPullDistance < 1f)
            {
                currentPullDistance = 0;
                break;
            }

            yield return null;
        }

        reloadIndicator.SetActive(false);

        if (indicatorRect != null)
            indicatorRect.localPosition = originalIndicatorPosition;
    }

    public void CompleteReload()
    {
        if (!isRefreshing) return;

        isRefreshing = false;
        hasTriggeredReload = false;

        StartCoroutine(SnapBackIndicator());
    }

    public void TriggerReload()
    {
        if (isRefreshing) return;

        currentPullDistance = pullThreshold + 10f;
        StartReload();
    }

    void OnDestroy()
    {
        if (scrollRect != null)
            scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
    }
}