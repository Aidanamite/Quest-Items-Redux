using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UI;
using I2.Loc;
using System.Runtime.CompilerServices;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;


namespace QuestItemsRedux
{
    public class Main : Mod
    {
        static RectTransform prefabParent;
        static GameObject uiPrefab;
        static QuestItemOption itemPrefab;
        static GameObject CurrentUI;
        static Sprite AddIcon;
        static Sprite SubIcon;
        static Sprite BackIcon;
        public static string hotkey;
        public static bool HotkeyDown => ExtraSettingsAPI_Loaded ? MyInput.GetButtonDown(hotkey) : Input.GetKeyDown(KeyCode.F7);
        public static List<Object> created = new List<Object>();
        public static MenuType ownUI = (MenuType)15;
        Harmony harmony;
        public void Start()
        {
            prefabParent = new GameObject("prefabParent", typeof(RectTransform)).GetComponent<RectTransform>();
            DontDestroyOnLoad(prefabParent);
            prefabParent.gameObject.SetActive(false);
            created.Add(prefabParent.gameObject);

            AddIcon = GetIcon("add");
            SubIcon = GetIcon("sub");
            BackIcon = GetIcon("back");

            var OptionMenuParent = Traverse.Create(ComponentManager<Settings>.Value).Field("optionsCanvas").GetValue<GameObject>().transform.Find("OptionMenuParent");
            var background = OptionMenuParent.Find("BrownBackground");
            var closeButton = OptionMenuParent.Find("CloseButton").GetComponent<Button>();
            var contentBox = OptionMenuParent.Find("TabContent/Graphics");

            uiPrefab = new GameObject("QuestItemGiverUI", typeof(RectTransform), typeof(GraphicRaycaster));
            uiPrefab.GetComponent<GraphicRaycaster>().enabled = false;
            var uiRect = uiPrefab.GetComponent<RectTransform>();
            uiRect.SetParent(prefabParent, false);
            uiRect.anchorMin = Vector2.one * 0.5f;
            uiRect.anchorMax = Vector2.one * 0.5f;
            var optionsSize = OptionMenuParent.GetComponent<RectTransform>().sizeDelta;
            uiRect.offsetMin = -optionsSize / 2;
            uiRect.offsetMax = optionsSize / 2;
            var newBackground = Instantiate(background, uiRect, false).GetComponent<RectTransform>();
            newBackground.name = background.name;
            newBackground.anchorMin = Vector2.zero;
            newBackground.anchorMax = Vector2.one;
            newBackground.offsetMin = Vector2.zero;
            newBackground.offsetMax = Vector2.zero;
            var newClose = Instantiate(closeButton, uiRect, false);
            newClose.name = closeButton.name;
            newClose.onClick = new Button.ButtonClickedEvent();
            var newCloseRect = newClose.GetComponent<RectTransform>();
            var closeSize = newCloseRect.sizeDelta;
            newCloseRect.anchorMin = Vector2.one;
            newCloseRect.anchorMax = Vector2.one;
            newCloseRect.offsetMin = -closeSize * 1.5f;
            newCloseRect.offsetMax = -closeSize / 2;
            var newContent = Instantiate(contentBox, uiRect, false).GetComponent<RectTransform>();
            newContent.name = "Container";
            newContent.gameObject.SetActive(true);
            newContent.anchorMin = Vector2.zero;
            newContent.anchorMax = Vector2.one;
            newContent.offsetMin = closeSize / 2;
            newContent.offsetMax = closeSize * new Vector2(-0.5f, -2f);
            DestroyImmediate(newContent.GetComponent<GraphicsSettingsBox>());
            var fitter = newContent.Find("Viewport/Content").gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.MinSize;
            var scroll = newContent.Find("Scrollbar Vertical").GetComponent<Scrollbar>();
            newContent.GetComponent<ScrollRect>().verticalScrollbar = scroll;
            scroll.value = 1;
            foreach (Transform t in newContent.Find("Viewport/Content"))
                Destroy(t.gameObject);

            itemPrefab = Instantiate(OptionMenuParent.Find("TabContent/Controls/Viewport/Content/ResetKeybinds").gameObject, prefabParent, false).AddComponent<QuestItemOption>();
            itemPrefab.name = "Item";
            itemPrefab.text = Instantiate(OptionMenuParent.Find("TabContent/General/Viewport/Content/Quick build/Text"), itemPrefab.transform).GetComponent<Text>();
            itemPrefab.text.name = "Text";

            itemPrefab.sub = itemPrefab.transform.Find("ResetKeybindsButton").GetComponent<Button>();
            itemPrefab.sub.name = "SubButton";
            itemPrefab.sub.onClick = new Button.ButtonClickedEvent();
            var btnCnt = itemPrefab.sub.transform.Find("Text").gameObject;
            DestroyImmediate(btnCnt.GetComponent<Text>());
            btnCnt.AddComponent<Image>().sprite = SubIcon;
            btnCnt.name = "Icon";
            var rct = itemPrefab.sub.GetComponent<RectTransform>();
            var s = rct.sizeDelta.y;
            rct.anchorMin = new Vector2(1, 0.5f);
            rct.anchorMax = new Vector2(1, 0.5f);
            rct.offsetMax = new Vector2(-s * 0.2f, s / 2);
            rct.offsetMin = new Vector2(-s * 1.2f, -s / 2);
            rct = btnCnt.GetComponent<RectTransform>();
            rct.offsetMin = Vector2.zero;
            rct.offsetMax = Vector2.zero;
            rct.anchorMin = Vector2.zero;
            rct.anchorMax = Vector2.one;

            itemPrefab.add = Instantiate(itemPrefab.sub, itemPrefab.sub.transform.parent);
            itemPrefab.add.name = "AddButton";
            rct = itemPrefab.add.GetComponent<RectTransform>();
            s = rct.sizeDelta.x * 1.2f;
            rct.offsetMin -= new Vector2(s, 0);
            rct.offsetMax -= new Vector2(s, 0);
            itemPrefab.add.transform.Find("Icon").GetComponent<Image>().sprite = AddIcon;

            itemPrefab.back = new GameObject("IconBack").AddComponent<Image>();
            itemPrefab.back.transform.SetParent(itemPrefab.transform, false);
            rct = itemPrefab.back.GetComponent<RectTransform>();
            rct.anchorMin = new Vector2(0, 0.5f);
            rct.anchorMax = new Vector2(0, 0.5f);
            var rct2 = itemPrefab.text.GetComponent<RectTransform>();
            rct.offsetMin = new Vector2(rct2.offsetMin.x, -rct2.sizeDelta.y / 2 * 1.5f);
            rct.offsetMax = new Vector2(rct2.offsetMin.x + rct2.sizeDelta.y * 1.5f, rct2.sizeDelta.y / 2 * 1.5f);
            rct2.offsetMin += new Vector2(rct.sizeDelta.y * 1.1f, 0);
            itemPrefab.back.sprite = BackIcon;

            itemPrefab.icon = new GameObject("Icon").AddComponent<Image>();
            itemPrefab.icon.transform.SetParent(itemPrefab.back.transform, false);
            rct = itemPrefab.icon.GetComponent<RectTransform>();
            rct.anchorMin = new Vector2(0.25f, 0.25f);
            rct.anchorMax = new Vector2(0.75f, 0.75f);
            rct.offsetMin = Vector2.zero;
            rct.offsetMax = Vector2.zero;
            DestroyLocalizations(itemPrefab.gameObject);

            if (ComponentManager<CanvasHelper>.Value) Patch_CanvasHelper_Create.Prefix(ComponentManager<CanvasHelper>.Value);
            (harmony = new Harmony("com.aidanamite.QuestItemsRedux")).PatchAll();

            Log("Mod has been loaded!");
        }

        public void OnModUnload()
        {
            CloseMenu();

            if (ComponentManager<CanvasHelper>.Value)
            {
                var menusT = Traverse.Create(ComponentManager<CanvasHelper>.Value).Field<GameMenu[]>("gameMenus");
                var menus = new List<GameMenu>(menusT.Value);
                menus.RemoveAll((x) => x.menuType == ownUI);
                menusT.Value = menus.ToArray();
            }

            foreach (var o in created)
                if (o)
                    Destroy(o);

            harmony?.UnpatchAll(harmony.Id);
            Log("Mod has been unloaded!");
        }

        void Update()
        {
            if (CanvasHelper.ActiveMenu == MenuType.None && HotkeyDown && RAPI.GetLocalPlayer())
                OpenUI();
            else if (CanvasHelper.ActiveMenu == ownUI && (MyInput.GetButtonDown("Cancel") || HotkeyDown))
                CurrentUI?.transform.Find("CloseButton").GetComponent<Button>().onClick.Invoke();
        }

        public static void DestroyLocalizations(GameObject gO)
        {
            foreach (Localize localize in gO.GetComponentsInChildren<Localize>())
                DestroyImmediate(localize, true);
        }
        public static void OpenUI()
        {
            if (CurrentUI)
                Destroy(CurrentUI);
            if (CanvasHelper.ActiveMenu != ownUI)
                ComponentManager<CanvasHelper>.Value.OpenMenu(ownUI);
            CurrentUI = Instantiate(uiPrefab, null, false);
            created.Add(CurrentUI);
            CurrentUI.transform.SetParent(ComponentManager<CanvasHelper>.Value.transform, false);
            CurrentUI.GetComponent<GraphicRaycaster>().enabled = true;
            var close = CurrentUI.transform.Find("CloseButton").GetComponent<Button>();
            close.onClick.AddListener(CloseMenu);
            var content = CurrentUI.transform.Find("Container/Viewport/Content");
            var alt = false;
            foreach (var f in QuestItemManager.QuestItems)
            {
                var o = Instantiate(itemPrefab, content, false);
                o.SetItem(f.SOQuestItem.questItemType);
                if (o && o.GetComponent<Image>())
                    o.GetComponent<Image>().enabled = alt;
                alt = !alt;
                    
            }
        }

        static void CloseMenu()
        {
            if (CurrentUI)
                Destroy(CurrentUI);
            if (ComponentManager<CanvasHelper>.Value)
                ComponentManager<CanvasHelper>.Value.CloseMenu(ownUI);
        }


        Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
        public Sprite GetIcon(string image) => icons.TryGetValue(image, out var s) ? s : icons[image] = LoadImage(image + ".png", true).ToSprite();
        public Texture2D LoadImage(string filename, bool removeMipMaps = false, FilterMode? mode = null, bool leaveReadable = true)
        {
            var t = new Texture2D(0, 0);
            t.LoadImage(GetEmbeddedFileBytes(filename), !removeMipMaps && !leaveReadable);
            if (removeMipMaps)
            {
                var t2 = new Texture2D(t.width, t.height, t.format, false);
                t2.SetPixels(t.GetPixels(0));
                t2.Apply(false, !leaveReadable);
                Destroy(t);
                t = t2;
            }
            if (mode != null)
                t.filterMode = mode.Value;
            created.Add(t);
            return t;
        }


        [ConsoleCommand("questitems_menu", "Opens menu for giving yourself quest items")]
        public static void MyCommand(string[] args)
        {
            if (RAPI.GetLocalPlayer())
                OpenUI();
            else
                Debug.LogWarning("Menu can only be opened in world");
        }


        static bool ExtraSettingsAPI_Loaded = false;
        public void ExtraSettingsAPI_Load()
        {
            hotkey = ExtraSettingsAPI_GetKeybindName("hotkey");
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string ExtraSettingsAPI_GetKeybindName(string SettingName) => null;
    }

    [HarmonyPatch(typeof(CanvasHelper), "Awake")]
    class Patch_CanvasHelper_Create
    {
        public static void Prefix(CanvasHelper __instance)
        {
            var menusT = Traverse.Create(__instance).Field<GameMenu[]>("gameMenus");
            var menus = new List<GameMenu>(menusT.Value);
            menus.Add(new GameMenu()
            {
                menuType = Main.ownUI,
                canvasRaycaster = null,
                menuObjects = new List<GameObject>(),
                messageReciever = null,
                recieveEventMessages = false
            });
            menusT.Value = menus.ToArray();
        }
    }

    public class QuestItemOption : MonoBehaviour
    {
        public Button add;
        public Button sub;
        public Image icon;
        public Text text;
        public Image back;
        QuestItemType type;

        static SO_QuestItemColors _c;
        static SO_QuestItemColors Colors
        {
            get
            {
                if (!_c)
                {
                    var p = RAPI.GetLocalPlayer();
                    if (!p)
                        return null;
                    var n = p.NoteBookUI;
                    if (!n)
                        return null;
                    _c = Traverse.Create(n).Field("questItemColors").GetValue<SO_QuestItemColors>();
                }
                return _c;
            }
        }

        public void SetItem(QuestItemType Item)
        {
            var i = QuestItemManager.GetQuestItemFromType(Item);
            if (i?.SOQuestItem)
            {
                type = Item;
                QuestItemManager.OnQuestItemChange += OnChange;
                icon.sprite = i.SOQuestItem.itemImage;
                back.color = Colors.GetQuestItemColorFromDestination(i.SOQuestItem.destinationType);
                OnChange(i, 0, false);
                add.onClick = new Button.ButtonClickedEvent();
                add.onClick.AddListener(() => ComponentManager<QuestItemManager>.Value.AddQuestItemsNetworked(false, new QuestItem(i.SOQuestItem, 1)));
                sub.onClick = new Button.ButtonClickedEvent();
                sub.onClick.AddListener(() => ComponentManager<QuestItemManager>.Value.AddQuestItemsNetworked(false, new QuestItem(i.SOQuestItem, -1)));
            }
        }

        void OnDestroy() => QuestItemManager.OnQuestItemChange -= OnChange;

        void OnChange(QuestItem item, int change, bool notify)
        {
            if (item.SOQuestItem.questItemType == type)
            {
                text.text = item.SOQuestItem.DisplayName + " [Current: " + item.itemCount + "]";
                text.rectTransform.offsetMax += new Vector2(text.preferredWidth - text.rectTransform.sizeDelta.x, 0);
            }
        }
    }

    public static class ExtentionMethods
    {
        public static Sprite ToSprite(this Texture2D texture, Rect? rect = null, Vector2? pivot = null)
        {
            var s = Sprite.Create(texture, rect ?? new Rect(0, 0, texture.width, texture.height), pivot ?? new Vector2(0.5f, 0.5f));
            Main.created.Add(s);
            return s;
        }
    }
}