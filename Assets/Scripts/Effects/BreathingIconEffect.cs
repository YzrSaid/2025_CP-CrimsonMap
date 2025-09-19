using UnityEngine;

public class BreathingIconEffect : MonoBehaviour
{
    [Header("Breathing Animation Settings")]
    [Range(0.8f, 1.2f)]
    public float minScale = 0.98f;          
    
    [Range(0.8f, 1.2f)]
    public float maxScale = 1.02f;         
    
    [Range(0.5f, 3.0f)]
    public float breathingSpeed = 0.8f;    
    
    [Header("Animation Style")]
    public AnimationCurve breathingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);  
    
    private Vector3 originalScale;
    private float time;
    
    void Start()
    {
        originalScale = transform.localScale;
    }
    
    void Update()
    {
        time += Time.deltaTime * breathingSpeed;
        
        float triangleWave = Mathf.PingPong(time, 1f);
        
        float smoothTime = Mathf.SmoothStep(0f, 1f, triangleWave);
        
        float currentScale = Mathf.Lerp(minScale, maxScale, smoothTime);
        
        transform.localScale = originalScale * currentScale;
    }
    
    void OnEnable()
    {
        // Reset time
        time = 0f;
    }
}