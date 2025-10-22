using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class RouteItem : MonoBehaviour
{
    [Header( "UI Elements" )]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI distanceText;
    public TextMeshProUGUI walkingTimeText;
    public TextMeshProUGUI viaModeText;
    public TextMeshProUGUI pathInfoText;
    public Button itemButton;

    [Header( "Path Toggle" )]
    public Button togglePathButton;
    public TextMeshProUGUI toggleButtonText;
    public GameObject openIcon;
    public GameObject closeIcon;

    [Header( "Visual Feedback" )]
    public Image backgroundImage;
    public Outline blackOutline;
    public Outline redOutline;
    public Color normalColor = new Color( 1f, 1f, 1f, 0.1f );
    public Color selectedColor = new Color( 0.2f, 0.8f, 0.3f, 0.3f );

    private int routeIndex;
    private System.Action<int> onRouteSelected;
    private bool isSelected = false;
    private bool isPathVisible = false;

    public void Initialize( int index, RouteData routeData, System.Action<int> selectCallback )
    {
        routeIndex = index;
        onRouteSelected = selectCallback;

        if ( titleText != null ) {
            titleText.text = $"Route #{index + 1}";
        }

        if ( distanceText != null ) {
            distanceText.text = $"<b>Distance:</b> {routeData.formattedDistance}";
        }

        if ( walkingTimeText != null ) {
            walkingTimeText.text = $"<b>Time:</b> ~{routeData.walkingTime}";
        }

        if ( viaModeText != null ) {
            viaModeText.text = $"<b>Route:</b> {routeData.viaMode}";
        }

        if ( pathInfoText != null ) {
            string pathInfo = $"<b>Path ({routeData.path.Count} stops):</b>\n";
            for ( int i = 0; i < routeData.path.Count; i++ ) {
                var node = routeData.path[i].node;
                pathInfo += $"{i + 1}. {node.name}\n";
            }
            pathInfoText.text = pathInfo;
        }

        if ( itemButton != null ) {
            itemButton.onClick.RemoveAllListeners();
            itemButton.onClick.AddListener( OnItemClicked );
        }

        // Setup toggle button
        if ( togglePathButton != null ) {
            togglePathButton.onClick.RemoveAllListeners();
            togglePathButton.onClick.AddListener( TogglePathVisibility );
        }

        SetPathVisibility( false );

        SetSelected( false );

        if ( blackOutline != null ) {
            blackOutline.effectColor = HexToColor( "4B4B4B" );
            blackOutline.enabled = true;
        }

        if ( redOutline != null ) {
            redOutline.effectColor = HexToColor( "B81013" );
            redOutline.enabled = false;
        }
    }
    private Color HexToColor( string hex )
    {
        hex = hex.Replace( "#", "" );

        byte r = byte.Parse( hex.Substring( 0, 2 ), System.Globalization.NumberStyles.HexNumber );
        byte g = byte.Parse( hex.Substring( 2, 2 ), System.Globalization.NumberStyles.HexNumber );
        byte b = byte.Parse( hex.Substring( 4, 2 ), System.Globalization.NumberStyles.HexNumber );

        return new Color32( r, g, b, 255 );
    }

    private void OnItemClicked()
    {
        onRouteSelected?.Invoke( routeIndex );
    }

    private void TogglePathVisibility()
    {
        isPathVisible = !isPathVisible;
        SetPathVisibility( isPathVisible );
    }

    private void SetPathVisibility( bool visible )
    {
        isPathVisible = visible;

        // Show/hide the container
        if ( pathInfoText != null ) {
            pathInfoText.gameObject.SetActive( visible );
        }

        // Update button text
        if ( toggleButtonText != null ) {
            toggleButtonText.text = visible ? "Hide Paths" : "Show Full Path";
        }

        // Toggle icons
        if ( openIcon != null ) {
            openIcon.SetActive( !visible ); // Show open icon when path is hidden
        }

        if ( closeIcon != null ) {
            closeIcon.SetActive( visible ); // Show close icon when path is visible
        }

        // Force layout rebuild
        StartCoroutine( RefreshLayoutNextFrame() );
    }

    private IEnumerator RefreshLayoutNextFrame()
    {
        // Wait for end of frame to ensure all UI changes are processed
        yield return new WaitForEndOfFrame();

        // Force rebuild the layout starting from this object up to the parent
        LayoutRebuilder.ForceRebuildLayoutImmediate( GetComponent<RectTransform>() );

        // Also rebuild parent if it exists (in case it's in a Vertical/Horizontal Layout Group)
        if ( transform.parent != null ) {
            RectTransform parentRect = transform.parent.GetComponent<RectTransform>();
            if ( parentRect != null ) {
                LayoutRebuilder.ForceRebuildLayoutImmediate( parentRect );
            }
        }
    }

    public void SetSelected( bool selected )
    {
        isSelected = selected;

        if ( backgroundImage != null ) {
            backgroundImage.color = selected ? selectedColor : normalColor;
        }

        // Toggle between black and red outlines
        if ( blackOutline != null ) {
            blackOutline.enabled = !selected;  // Black OFF when selected
        }

        if ( redOutline != null ) {
            redOutline.enabled = selected;     // Red ON when selected
        }
    }

    public bool IsSelected()
    {
        return isSelected;
    }

    void OnDestroy()
    {
        if ( itemButton != null ) {
            itemButton.onClick.RemoveAllListeners();
        }

        if ( togglePathButton != null ) {
            togglePathButton.onClick.RemoveAllListeners();
        }
    }
}