using UnityEngine;

public class BreathingIconEffect : MonoBehaviour
{
    [Header("Breathing Animation Settings")]
    [Range(0.8f, 1.2f)]
    public float minScale = 0.98f;          // Minimum scale (breathe in)
    
    [Range(0.8f, 1.2f)]
    public float maxScale = 1.02f;          // Maximum scale (breathe out)
    
    [Range(0.5f, 3.0f)]
    public float breathingSpeed = 0.8f;     // Speed of breathing (slower for subtle effect)
    
    [Header("Animation Style")]
    public AnimationCurve breathingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);  // Curve for smooth breathing
    
    private Vector3 originalScale;
    private float time;
    
    void Start()
    {
        // Store the original scale of the icon
        originalScale = transform.localScale;
    }
    
    void Update()
    {
        // Increment time based on breathing speed
        time += Time.deltaTime * breathingSpeed;
        
        // Use a triangle wave for perfectly linear movement (no curves, no pauses)
        float triangleWave = Mathf.PingPong(time, 1f);
        
        // Apply smoothstep for just a tiny bit of easing (optional)
        float smoothTime = Mathf.SmoothStep(0f, 1f, triangleWave);
        
        // Interpolate between min and max scale
        float currentScale = Mathf.Lerp(minScale, maxScale, smoothTime);
        
        // Apply the new scale while maintaining the original proportions
        transform.localScale = originalScale * currentScale;
    }
    
    void OnEnable()
    {
        // Reset time when the object becomes active
        time = 0f;
    }
}