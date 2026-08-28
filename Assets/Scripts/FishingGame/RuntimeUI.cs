using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RustyFishing
{
    public static class RuntimeUI
    {
        public static Sprite Sprite(string path)
        {
            var sprite=DirectReskinSprites.Load(path);
            if(sprite==null)sprite=Resources.Load<Sprite>("Art/"+path);
            if(sprite==null)Debug.LogError($"Missing UI sprite: Assets/Resources/Art/{path}.png");
            return sprite;
        }
        // A plain 1x1 white sprite, built once. Use it for solid-colour quads (e.g. the fishing line
        // segments) so they render as clean tinted rectangles instead of stretching a detailed sprite.
        static Sprite whiteSprite;
        public static Sprite WhiteSprite()
        {
            if(whiteSprite==null){var tex=Texture2D.whiteTexture;whiteSprite=UnityEngine.Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,.5f),100f);whiteSprite.name="RuntimeWhite";}
            return whiteSprite;
        }
        public static RectTransform Rect(Transform parent,string name,Vector2 pos,Vector2 size)
        { var go=new GameObject(name,typeof(RectTransform));var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;return r; }
        public static Image Image(Transform parent,string name,string path,Vector2 pos,Vector2 size)
        {var r=Rect(parent,name,pos,size);var i=r.gameObject.AddComponent<Image>();i.sprite=Sprite(path);i.preserveAspect=true;return i;}
        static TMP_FontAsset pixelFont;
        public static TMP_Text Text(Transform parent,string value,Vector2 pos,Vector2 size,int fontSize=34,TextAnchor align=TextAnchor.MiddleCenter,Color? color=null)
        {var r=Rect(parent,"Text",pos,size);var t=r.gameObject.AddComponent<TextMeshProUGUI>();if(pixelFont==null)pixelFont=Resources.Load<TMP_FontAsset>("Fonts/PixelifySans/Pixelify Sans Bold SDF");if(pixelFont!=null)t.font=pixelFont;t.text=value;t.fontSize=fontSize;t.fontStyle=FontStyles.Normal;t.alignment=ToTMP(align);t.color=color??new Color(.11f,.15f,.14f);t.enableAutoSizing=true;t.fontSizeMin=12;t.fontSizeMax=fontSize;t.raycastTarget=false;return t;}
        static TextAlignmentOptions ToTMP(TextAnchor a)=>a switch{
            TextAnchor.UpperLeft=>TextAlignmentOptions.TopLeft,TextAnchor.UpperCenter=>TextAlignmentOptions.Top,TextAnchor.UpperRight=>TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft=>TextAlignmentOptions.Left,TextAnchor.MiddleCenter=>TextAlignmentOptions.Center,TextAnchor.MiddleRight=>TextAlignmentOptions.Right,
            TextAnchor.LowerLeft=>TextAlignmentOptions.BottomLeft,TextAnchor.LowerCenter=>TextAlignmentOptions.Bottom,TextAnchor.LowerRight=>TextAlignmentOptions.BottomRight,
            _=>TextAlignmentOptions.Center};
        public static Button Button(Transform parent,string spritePath,string label,Vector2 pos,Vector2 size,System.Action action,int fontSize=34)
        {var i=Image(parent,label,spritePath,pos,size);var b=i.gameObject.AddComponent<Button>();b.targetGraphic=i;b.onClick.AddListener(()=>action());Text(i.transform,label,Vector2.zero,size*.76f,fontSize,TextAnchor.MiddleCenter,new Color(.94f,.9f,.76f));return b;}
        public static Canvas BuildCanvas()
        {var c=new GameObject("GameCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;var s=c.GetComponent<CanvasScaler>();s.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;s.referenceResolution=new Vector2(1080,1920);s.matchWidthOrHeight=.5f;if(Object.FindFirstObjectByType<EventSystem>()==null){var input=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>();input.AssignDefaultActions();}return c;}
    }
}
