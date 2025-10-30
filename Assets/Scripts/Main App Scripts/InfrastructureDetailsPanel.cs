using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InfrastructureDetailsPanel : MonoBehaviour
{
    [Header( "UI References" )]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI categoryText;
    public TextMeshProUGUI categoryLegendText;
    public Image infrastructureImage;
    public TextMeshProUGUI emailText;
    public TextMeshProUGUI phoneText;
    public TextMeshProUGUI latAndlngText;
    public TextMeshProUGUI campusText;
    public Button closeButton;

    [Header( "Background Settings" )]
    public string backgroundPanelName = "BackgroundForExplorePanel";

    private Infrastructure infrastructure;
    private Category category;
    private Node node;
    private CampusData campus;
    private Transform backgroundTransform;

    void Awake()
    {
        if ( closeButton != null )
            closeButton.onClick.AddListener( Close );

        SetupBackground();
    }

    private void SetupBackground()
    {
        backgroundTransform = transform.root.Find( backgroundPanelName );

        if ( backgroundTransform == null ) {
            Canvas canvas = FindObjectOfType<Canvas>();
            if ( canvas != null ) {
                backgroundTransform = SearchAllChildren( canvas.transform, backgroundPanelName );
            }
        }

        if ( backgroundTransform != null ) {
            backgroundTransform.gameObject.SetActive( true );
        }
    }

    private Transform SearchAllChildren( Transform parent, string name )
    {
        foreach ( Transform child in parent.GetComponentsInChildren<Transform>( true ) ) {
            if ( child.name == name ) {
                return child;
            }
        }
        return null;
    }

    public void SetData( Infrastructure infra, Category cat, Node nodeData, CampusData campusData )
    {
        infrastructure = infra;
        category = cat;
        node = nodeData;
        campus = campusData;
        PopulateUI();
    }

    void PopulateUI()
    {
        if ( titleText != null )
            titleText.text = infrastructure.name;

        if ( category != null ) {
            if ( categoryText != null )
                categoryText.text = category.name;

            // Display the legend (letter)
            if ( categoryLegendText != null && !string.IsNullOrEmpty( category.legend ) ) {
                categoryLegendText.text = category.legend;
            }
        }

        if ( !string.IsNullOrEmpty( infrastructure.image_url ) ) {
            Texture2D texture = Resources.Load<Texture2D>( infrastructure.image_url );
            if ( texture != null && infrastructureImage != null ) {
                infrastructureImage.sprite = Sprite.Create( texture, new Rect( 0, 0, texture.width, texture.height ), Vector2.one * 0.5f );
            }
        }

        if ( emailText != null )
            emailText.text = infrastructure.email;

        if ( phoneText != null )
            phoneText.text = infrastructure.phone;

        if ( node != null ) {
            if ( latAndlngText != null )
                latAndlngText.text = node.latitude.ToString( "F6" ) + " | " + node.longitude.ToString( "F6" );
        }

        if ( campus != null ) {
            if ( campusText != null )
                campusText.text = campus.campus_name;
        }
    }

    void Close()
    {
        Destroy( gameObject );
        if ( backgroundTransform != null ) {
            backgroundTransform.gameObject.SetActive( false );
        }
    }
}