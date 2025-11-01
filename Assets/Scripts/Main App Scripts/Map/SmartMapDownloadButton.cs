using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

public class SmartMapDownloadButton : MonoBehaviour
{
    [Header("UI References")]
    public Button downloadButton;
    public Button checkButton;
    public GameObject progressPanel;
    public Slider progressBar;
    public TextMeshProUGUI progressText;
    public Button cancelButton;
    public TextMeshProUGUI cancelButtonText;
    
    [Header("Delete Confirmation Panel")]
    public GameObject deleteConfirmPanel;
    public Button confirmDeleteButton;
    public Button cancelDeleteButton;
    public TextMeshProUGUI deleteMessageText;
    
    [Header("Background Panel")]
    public GameObject backgroundPanel;
    
    [Header("Animation Settings")]
    public float animationDuration = 0.3f;
    public Ease easeType = Ease.OutBack;
    
    [Header("Internet Retry Settings")]
    public float internetCheckInterval = 2f;
    public float maxWaitTimeForInternet = 20f;
    
    private OfflineCacheCoordinator coordinator;
    private MapboxOfflineManager offlineManager;
    private MapDropdown mapDropdown;
    private string currentMapId = "";
    private string lastCheckedMapId = "";
    private bool isDownloadCancelled = false;
    private bool isWaitingForInternet = false;
    
    private Dictionary<GameObject, Vector3> originalScales = new Dictionary<GameObject, Vector3>();
    
    void Start()
    {
        coordinator = FindObjectOfType<OfflineCacheCoordinator>();
        offlineManager = FindObjectOfType<MapboxOfflineManager>();
        mapDropdown = FindObjectOfType<MapDropdown>();
        
        if (coordinator == null || offlineManager == null)
        {
            return;
        }
        
        StoreOriginalScale(progressPanel);
        StoreOriginalScale(deleteConfirmPanel);
        
        if (downloadButton != null)
        {
            downloadButton.onClick.AddListener(OnDownloadClicked);
        }
        
        if (checkButton != null)
        {
            checkButton.onClick.AddListener(OnCheckClicked);
        }
        
        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
        }
        
        if (confirmDeleteButton != null)
        {
            confirmDeleteButton.onClick.AddListener(OnConfirmDelete);
        }
        
        if (cancelDeleteButton != null)
        {
            cancelDeleteButton.onClick.AddListener(OnCancelDelete);
        }
        
        if (progressPanel != null)
        {
            progressPanel.SetActive(false);
        }
        
        if (deleteConfirmPanel != null)
        {
            deleteConfirmPanel.SetActive(false);
        }
        
        if (backgroundPanel != null)
        {
            backgroundPanel.SetActive(false);
        }
        
        offlineManager.OnCacheProgress += OnCacheProgress;
        offlineManager.OnCacheComplete += OnCacheComplete;
        offlineManager.OnCacheError += OnCacheError;
        
        StartCoroutine(WaitAndUpdateButtons());
    }
    
    void StoreOriginalScale(GameObject panel)
    {
        if (panel != null && !originalScales.ContainsKey(panel))
        {
            originalScales.Add(panel, panel.transform.localScale);
        }
    }
    
    System.Collections.IEnumerator WaitAndUpdateButtons()
    {
        yield return new WaitUntil(() => MapManager.Instance != null && MapManager.Instance.IsReady());
        
        yield return new WaitUntil(() => coordinator.isInitialized);
        
        MapInfo currentMap = MapManager.Instance.GetCurrentMap();
        if (currentMap != null)
        {
            currentMapId = currentMap.map_id;
            lastCheckedMapId = currentMapId;
            coordinator.SetCurrentMap(currentMapId);
        }
        
        UpdateButtonStates();
    }
    
    void OnDestroy()
    {
        if (offlineManager != null)
        {
            offlineManager.OnCacheProgress -= OnCacheProgress;
            offlineManager.OnCacheComplete -= OnCacheComplete;
            offlineManager.OnCacheError -= OnCacheError;
        }
        
        if (progressPanel != null)
        {
            progressPanel.transform.DOKill();
        }
        
        if (deleteConfirmPanel != null)
        {
            deleteConfirmPanel.transform.DOKill();
        }
    }
    
    void OpenPanel(GameObject panel, GameObject background = null)
    {
        if (panel == null) return;
        
        GameObject bgToUse = background ?? backgroundPanel;
        
        if (bgToUse != null && !bgToUse.activeSelf)
        {
            bgToUse.SetActive(true);
        }
        
        panel.SetActive(true);
        
        panel.transform.localScale = Vector3.zero;
        
        Vector3 targetScale = originalScales.ContainsKey(panel) ? originalScales[panel] : Vector3.one;
        panel.transform.DOScale(targetScale, animationDuration)
            .SetEase(easeType)
            .SetUpdate(true);
    }
    
    void ClosePanel(GameObject panel, GameObject background = null)
    {
        if (panel == null) return;
        
        GameObject bgToUse = background ?? backgroundPanel;
        
        panel.transform.DOScale(Vector3.zero, animationDuration)
            .SetEase(Ease.InBack)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                panel.SetActive(false);
                
                if (bgToUse != null)
                {
                    bool otherPanelsActive = false;
                    
                    if (progressPanel != null && progressPanel != panel && progressPanel.activeSelf)
                        otherPanelsActive = true;
                    
                    if (deleteConfirmPanel != null && deleteConfirmPanel != panel && deleteConfirmPanel.activeSelf)
                        otherPanelsActive = true;
                    
                    if (!otherPanelsActive)
                    {
                        bgToUse.SetActive(false);
                    }
                }
            });
    }
    
    public void OnMapChanged(string newMapId)
    {
        if (coordinator != null)
        {
            coordinator.SetCurrentMap(newMapId);
            currentMapId = newMapId;
            lastCheckedMapId = newMapId;
            UpdateButtonStates();
        }
    }
    
    void UpdateButtonStates()
    {
        if (string.IsNullOrEmpty(currentMapId)) return;
        
        bool isDownloaded = coordinator.IsMapDownloaded(currentMapId);
        bool isDownloading = offlineManager.isCaching;
        bool hasInternet = Application.internetReachability != NetworkReachability.NotReachable;
        
        if (downloadButton != null)
        {
            downloadButton.gameObject.SetActive(!isDownloaded);
            downloadButton.interactable = !isDownloading && hasInternet;
        }
        
        if (checkButton != null)
        {
            checkButton.gameObject.SetActive(isDownloaded);
            checkButton.interactable = !isDownloading;
        }
    }
    
    void OnDownloadClicked()
    {
        if (string.IsNullOrEmpty(currentMapId)) return;
        
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            OpenPanel(progressPanel);
            
            if (progressText != null)
            {
                progressText.text = "No Internet Connection\nTry Again";
            }
            
            if (progressBar != null)
            {
                progressBar.value = 0f;
            }
            
            if (cancelButtonText != null)
            {
                cancelButtonText.text = "Okay";
            }
            
            return;
        }
        
        isDownloadCancelled = false;
        isWaitingForInternet = false;
        
        OpenPanel(progressPanel);
        
        if (cancelButtonText != null)
        {
            cancelButtonText.text = "Cancel";
        }
        
        coordinator.DownloadMapForOffline(currentMapId);
        
        StartCoroutine(MonitorInternetDuringDownload());
        
        UpdateButtonStates();
    }
    
    void OnCheckClicked()
    {
        OpenPanel(deleteConfirmPanel);
        
        if (deleteMessageText != null && MapManager.Instance != null)
        {
            MapInfo currentMap = MapManager.Instance.GetCurrentMap();
            if (currentMap != null)
            {
                deleteMessageText.text = $"Delete {currentMap.map_name} offline cache?\n\nYou'll need internet to download it again.";
            }
            else
            {
                deleteMessageText.text = "Delete offline cache?\n\nYou'll need internet to download it again.";
            }
        }
    }
    
    void OnCancelClicked()
    {
        if (isWaitingForInternet || Application.internetReachability == NetworkReachability.NotReachable)
        {
            ClosePanel(progressPanel);
            
            isWaitingForInternet = false;
            isDownloadCancelled = false;
            
            UpdateButtonStates();
            return;
        }
        
        isDownloadCancelled = true;
        
        StopAllCoroutines();
        
        ClosePanel(progressPanel);
        
        UpdateButtonStates();
        
        ShowToast("Download cancelled");
    }
    
    System.Collections.IEnumerator MonitorInternetDuringDownload()
    {
        float timeWithoutInternet = 0f;
        
        while (offlineManager.isCaching && !isDownloadCancelled)
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                if (!isWaitingForInternet)
                {
                    isWaitingForInternet = true;
                    timeWithoutInternet = 0f;
                    
                    if (progressText != null)
                    {
                        progressText.text = "Internet connection lost\nWaiting...";
                    }
                }
                
                timeWithoutInternet += internetCheckInterval;
                
                if (progressText != null)
                {
                    int secondsLeft = Mathf.CeilToInt(maxWaitTimeForInternet - timeWithoutInternet);
                    progressText.text = $"Internet connection lost\nRetrying... ({secondsLeft}s)";
                }
                
                if (timeWithoutInternet >= maxWaitTimeForInternet)
                {
                    StopAllCoroutines();
                    
                    if (progressText != null)
                    {
                        progressText.text = "No Internet Connection\nTry Again";
                    }
                    
                    if (progressBar != null)
                    {
                        progressBar.value = 0f;
                    }
                    
                    if (cancelButtonText != null)
                    {
                        cancelButtonText.text = "Okay";
                    }
                    
                    yield break;
                }
            }
            else
            {
                if (isWaitingForInternet)
                {
                    isWaitingForInternet = false;
                    
                    if (cancelButtonText != null)
                    {
                        cancelButtonText.text = "Cancel";
                    }
                }
            }
            
            yield return new WaitForSeconds(internetCheckInterval);
        }
    }
    
    void OnConfirmDelete()
    {
        if (string.IsNullOrEmpty(currentMapId)) return;
        
        coordinator.ClearCache(currentMapId);
        
        ClosePanel(deleteConfirmPanel);
        
        UpdateButtonStates();
        
        ShowToast("Offline cache deleted");
    }
    
    void OnCancelDelete()
    {
        ClosePanel(deleteConfirmPanel);
    }
    
    void OnCacheProgress(float progress)
    {
        if (!isWaitingForInternet)
        {
            if (progressBar != null)
            {
                progressBar.value = progress;
            }
            
            if (progressText != null && MapManager.Instance != null)
            {
                MapInfo currentMap = MapManager.Instance.GetCurrentMap();
                string mapName = currentMap != null ? currentMap.map_name : "Map";
                progressText.text = $"Downloading {mapName}...\n{progress * 100:F0}%";
            }
        }
    }
    
    void OnCacheComplete()
    {
        StopAllCoroutines();
        
        isDownloadCancelled = false;
        isWaitingForInternet = false;
        
        ClosePanel(progressPanel);
        
        if (cancelButtonText != null)
        {
            cancelButtonText.text = "Cancel";
        }
        
        UpdateButtonStates();
        
        ShowToast("Map downloaded successfully!");
    }
    
    void OnCacheError(string error)
    {
        StopAllCoroutines();
        
        if (isDownloadCancelled)
        {
            return;
        }
        
        isWaitingForInternet = false;
        
        bool isInternetError = error.Contains("internet") || error.Contains("connection") || 
                               Application.internetReachability == NetworkReachability.NotReachable;
        
        if (isInternetError)
        {
            if (progressText != null)
            {
                progressText.text = "No Internet Connection\nTry Again";
            }
            
            if (progressBar != null)
            {
                progressBar.value = 0f;
            }
            
            if (cancelButtonText != null)
            {
                cancelButtonText.text = "Okay";
            }
        }
        else
        {
            ClosePanel(progressPanel);
            
            if (cancelButtonText != null)
            {
                cancelButtonText.text = "Cancel";
            }
            
            ShowToast($"Download failed: {error}");
        }
        
        UpdateButtonStates();
    }
    
    void ShowToast(string message)
    {
    }
    
    void Update()
    {
        if (MapManager.Instance != null && MapManager.Instance.IsReady())
        {
            MapInfo currentMap = MapManager.Instance.GetCurrentMap();
            if (currentMap != null && currentMap.map_id != lastCheckedMapId)
            {
                currentMapId = currentMap.map_id;
                lastCheckedMapId = currentMapId;
                
                coordinator.SetCurrentMap(currentMapId);
                
                UpdateButtonStates();
            }
        }
        
        if (Time.frameCount % 60 == 0)
        {
            UpdateButtonStates();
        }
    }
}