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
    public GameObject reloadIndicator; // The pull-to-refresh indicator
    public TextMeshProUGUI reloadText;
    public Image reloadIcon;
    
    [Header("Settings")]
    public float pullThreshold = 150f; // How far to pull before reload triggers
    public float releaseThreshold = 100f; // Minimum pull distance to show indicator
    public float snapSpeed = 10f; // Speed of snap-back animation
    public bool enableHapticFeedback = true;
    
    [Header("Messages")]
    public string pullMessage = "Pull down to refresh";
    public string releaseMessage = "Release to refresh";
    public string loadingMessage = "Loading...";
    
    [Header("Events")]
    public UnityEvent OnReloadTriggered; // Event to trigger when reload happens
    
    // Private variables
    private bool isRefreshing = false;
    private bool isPulling = false;
    private bool hasTriggeredReload = false;
    private Vector2 lastTouchPosition;
    private float currentPullDistance = 0f;
    private Vector3 originalIndicatorPosition;
    private RectTransform indicatorRect;
    
    void Start()
    {
        // Auto-assign ScrollRect if not set
        if (scrollRect == null)
            scrollRect = GetComponent<ScrollRect>();
        
        if (contentTransform == null)
            contentTransform = scrollRect.content;
            
        // Setup reload indicator
        if (reloadIndicator != null)
        {
            indicatorRect = reloadIndicator.GetComponent<RectTransform>();
            originalIndicatorPosition = indicatorRect.localPosition;
            reloadIndicator.SetActive(false);
        }
        
        // Add scroll listener
        scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        
        // DEBUG: Log setup info
        Debug.Log($"🔧 ScrollToReload Setup:");
        Debug.Log($"   - ScrollRect: {(scrollRect != null ? "✅" : "❌")}");
        Debug.Log($"   - Content: {(contentTransform != null ? "✅" : "❌")}");
        Debug.Log($"   - ReloadIndicator: {(reloadIndicator != null ? "✅" : "❌")}");
        Debug.Log($"   - Initial scroll position: {scrollRect?.verticalNormalizedPosition}");
    }
    
    void Update()
    {
        HandleTouchInput();
        UpdateIndicatorPosition();
    }
    
    void HandleTouchInput()
    {
        if (isRefreshing) return;
        
        // Handle touch/mouse input using new Input System
        bool isTouching = false;
        Vector2 currentPosition = Vector2.zero;
        
        // Check for mouse/touch input
        if (Pointer.current != null && Pointer.current.press.isPressed)
        {
            isTouching = true;
            currentPosition = Pointer.current.position.ReadValue();
            
            // Check if this is the start of a press
            if (Pointer.current.press.wasPressedThisFrame)
            {
                Debug.Log($"🖱️ Pointer press started at: {currentPosition}");
                OnTouchStart(currentPosition);
            }
        }
        else if (Pointer.current != null && Pointer.current.press.wasReleasedThisFrame)
        {
            Debug.Log("🖱️ Pointer press released");
            OnTouchEnd();
        }
        
        // Handle touch movement
        if (isTouching && isPulling)
        {
            OnTouchMove(currentPosition);
        }
    }
    
    void OnTouchStart(Vector2 position)
    {
        lastTouchPosition = position;
        
        Debug.Log($"👆 Touch start at: {position}, scroll pos: {scrollRect.verticalNormalizedPosition:F2}");
        
        // Only start pulling if we're at the top of the scroll view
        // verticalNormalizedPosition: 1.0 = top, 0.0 = bottom
        if (scrollRect.verticalNormalizedPosition >= 0.99f)
        {
            isPulling = true;
            hasTriggeredReload = false;
            Debug.Log("👆 Started pulling!");
        }
        else
        {
            Debug.Log("👆 Not at top - can't pull to refresh");
        }
    }
    
    void OnTouchMove(Vector2 position)
    {
        if (!isPulling) return;
        
        float deltaY = position.y - lastTouchPosition.y;
        
        Debug.Log($"👆 Touch move - deltaY: {deltaY:F1}, pull distance: {currentPullDistance:F1}");
        
        // Only register downward pulls when at top (negative deltaY = pulling down in screen coordinates)
        if (deltaY < 0 && scrollRect.verticalNormalizedPosition >= 0.99f)
        {
            currentPullDistance += Mathf.Abs(deltaY) * 0.5f; // Use absolute value and dampen
            currentPullDistance = Mathf.Max(0, currentPullDistance);
            
            Debug.Log($"👆 Pull distance updated: {currentPullDistance:F1}");
            
            // Show indicator when pull threshold is reached
            if (currentPullDistance > releaseThreshold && reloadIndicator != null && !reloadIndicator.activeInHierarchy)
            {
                Debug.Log("📱 Showing reload indicator!");
                reloadIndicator.SetActive(true);
                if (enableHapticFeedback)
                {
                    // Light haptic feedback when indicator appears
                    #if UNITY_ANDROID || UNITY_IOS
                    Handheld.Vibrate();
                    #endif
                }
            }
            
            // Update message based on pull distance
            UpdateReloadMessage();
            
            // Trigger reload if pulled far enough
            if (currentPullDistance > pullThreshold && !hasTriggeredReload)
            {
                Debug.Log("🎯 Reload threshold reached!");
                hasTriggeredReload = true;
                if (enableHapticFeedback)
                {
                    // Stronger haptic feedback when reload triggers
                    #if UNITY_ANDROID || UNITY_IOS
                    Handheld.Vibrate();
                    #endif
                }
            }
        }
        else if (deltaY > 0)
        {
            Debug.Log($"👆 Pulling UP (deltaY: {deltaY:F1}) - ignoring");
        }
        else
        {
            Debug.Log($"👆 Pulling DOWN but not at top (deltaY: {deltaY:F1}) - ignoring");
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
        // DEBUG: Log scroll position
        Debug.Log($"📜 Scroll position: {scrollRect.verticalNormalizedPosition:F2}");
        
        // Reset pull if user scrolls away from top
        if (scrollRect.verticalNormalizedPosition < 0.99f && isPulling)
        {
            Debug.Log("📜 Reset pull - scrolled away from top");
            isPulling = false;
            StartCoroutine(SnapBackIndicator());
        }
    }
    
    void UpdateIndicatorPosition()
    {
        if (reloadIndicator == null || indicatorRect == null) return;
        
        if (isPulling && currentPullDistance > releaseThreshold)
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
        
        // Update UI to loading state
        if (reloadText != null)
            reloadText.text = loadingMessage;
            
        // Rotate the reload icon
        if (reloadIcon != null)
            StartCoroutine(RotateReloadIcon());
        
        Debug.Log("🔄 Scroll to reload triggered!");
        
        // Trigger the reload event
        OnReloadTriggered?.Invoke();
        
        // Auto-complete after 3 seconds if not manually completed
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
        
        // Reset rotation
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
        
        // Hide indicator
        reloadIndicator.SetActive(false);
        
        // Reset indicator position
        if (indicatorRect != null)
            indicatorRect.localPosition = originalIndicatorPosition;
    }
    
    // Public method to complete the reload (call this from your reload logic)
    public void CompleteReload()
    {
        if (!isRefreshing) return;
        
        isRefreshing = false;
        hasTriggeredReload = false;
        
        // Snap back the indicator
        StartCoroutine(SnapBackIndicator());
        
        Debug.Log("✅ Reload completed!");
    }
    
    // Public method to manually trigger reload
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