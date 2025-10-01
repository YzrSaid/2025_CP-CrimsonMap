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
    private bool wasPressed = false; // Track press state

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
        // ONLY process input when actually needed
        if (isRefreshing) return;

        if (Pointer.current != null)
        {
            bool isPressed = Pointer.current.press.isPressed;

            // Only process on state changes or when actively pulling
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
                // Only track movement when actively pulling
                OnTouchMove(Pointer.current.position.ReadValue());
            }
        }

        // Only update indicator position when pulling
        if (isPulling)
        {
            UpdateIndicatorPosition();
        }
    }

    void OnTouchStart(Vector2 position)
    {
        lastTouchPosition = position;

        // Only start pulling if at top of scroll
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

        // Only process downward movement when at top
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

            // Update message based on pull distance
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

        // Trigger reload if threshold was met
        if (hasTriggeredReload && currentPullDistance > pullThreshold)
        {
            StartReload();
        }
        else
        {
            // Snap back to original position
            StartCoroutine(SnapBackIndicator());
        }
    }

    void OnScrollValueChanged(Vector2 value)
    {
        // Reset pull if user scrolls away from top
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
            // Move indicator down based on pull distance
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

        // Animate pull distance back to 0
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