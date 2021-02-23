using BepInEx;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace StarMapTools
{
    [BepInPlugin("sky.plugins.dsp.StarMapTools", "StarMapTools", "3.2")]
    public class StarMapTools: BaseUnityPlugin
    {
        GameObject prefab_StarMapToolsBasePanel;//资源
        GameObject ui_StarMapToolsBasePanel;
        Dropdown StarList;//恒星下拉列表
        Dropdown PlanetList;//星球下拉列表
        Text TitleText;//标题
        InputField InfoText;//详细信息
        Toggle LoadResAmount;//是否加载资源数量
        bool dataLoadOver = false;//是否加载完数据
        bool showGUI = false;//是否显示GUI
        bool loadingStarData = false;//是否在加载数据
        GalaxyData galaxy;//星图数据
        KeyCode switchGUIKey;//开关GUI的快捷键
        KeyCode tpKey;//tp的快捷键
        StarSearcher starSearcher = new StarSearcher();//恒星搜索器
        ScrollRect OptionsList;//选项列表
        Toggle SearchNextToggle;//是否开启连续搜索
        InputField DysonLuminoText;//最小光度输入框
        InputField DistanceText;//最远距离输入框
        Dropdown ResultList;//查询结果(用于显示)
        Button SearchButton;//查询按钮
        bool SearchNext=false;//是否刷新种子并搜索
        List<StarData> SerachResult;//查询结果
        static StarMapTools self;//this
        void Start()
        {
            Harmony.CreateAndPatchAll(typeof(StarMapTools), null);
            self = this;
            StarMapToolsTranslate.regAllTranslate();
            //加载资源
            switchGUIKey = Config.Bind<KeyCode>("config", "switchGUI", KeyCode.F1, "开关GUI的按键".getTranslate()).Value;
            tpKey = Config.Bind<KeyCode>("config", "tp", KeyCode.F2, "传送按键".getTranslate()).Value;
            var ab = AssetBundle.LoadFromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("StarMapTools.starmaptools"));
            prefab_StarMapToolsBasePanel = ab.LoadAsset<GameObject>("StarMapToolsBasePanel");
        }
        void Update()
        {
            if (dataLoadOver)
            {
                //根据按键更新showGUI
                if (Input.GetKeyDown(switchGUIKey))
                {
                    showGUI = !showGUI;
                }
                //根据showGUI更新GUI的显示
                if(showGUI && !ui_StarMapToolsBasePanel.activeSelf || !showGUI && ui_StarMapToolsBasePanel.activeSelf)
                {
                    ui_StarMapToolsBasePanel.SetActive(!ui_StarMapToolsBasePanel.activeSelf);
                }
                //核心内容
                if (showGUI)
                {
                    //判断是否处于新建游戏的状态
                    if (UIRoot.instance.galaxySelect.starmap.galaxyData != null)
                    {
                        if (SearchNext)
                        {
                            LoadResAmount.isOn = false;
                            UIRoot.instance.galaxySelect.Rerand();
                        }
                        //更新数据
                        if (galaxy != UIRoot.instance.galaxySelect.starmap.galaxyData)
                        {
                            TitleText.text = "新游戏模式".getTranslate();
                            galaxy = UIRoot.instance.galaxySelect.starmap.galaxyData;
                            StarList.ClearOptions();
                            ResultList.ClearOptions();
                            foreach (StarData star in galaxy.stars)
                            {
                                StarList.options.Add(new Dropdown.OptionData(star.name));
                            }
                            StarList.value = -1;
                            StarList.RefreshShownValue();
                            if (SearchNext)
                            {
                                SearchButton.onClick.Invoke();
                            }
                        }
                    }
                    //判断是否处于游戏中
                    else if (GameMain.galaxy != null)
                    {
                        SearchNext = false;
                        //更新数据
                        if (galaxy != GameMain.galaxy)
                        {
                            TitleText.text = "读档模式".getTranslate();
                            galaxy = GameMain.galaxy;
                            StarList.ClearOptions();
                            ResultList.ClearOptions();
                            foreach (StarData star in galaxy.stars)
                            {
                                StarList.options.Add(new Dropdown.OptionData(star.name));
                            }
                            StarList.value = -1;
                            StarList.RefreshShownValue();
                        }
                        //传送功能
                        if (Input.GetKeyDown(tpKey))
                        {
                            object target = galaxy.StarById(StarList.value + 1);
                            GameMain.data.ArriveStar((StarData)target);
                            if (PlanetList.value > 0)
                            {
                                target = ((StarData)target).planets[PlanetList.value - 1];
                            }
                            StartCoroutine(wait(target));
                        }
                    }
                    //等待数据加载
                    else if (TitleText.text != "等待数据".getTranslate())
                    {
                        TitleText.text = "等待数据".getTranslate();
                        StarList.ClearOptions();
                        PlanetList.ClearOptions();
                        ResultList.ClearOptions();
                        InfoText.text = "";
                        SearchNext = false;
                    }
                    if (loadingStarData)
                    {
                        var star = galaxy.StarById(StarList.value + 1);
                        if (star.loaded || !LoadResAmount.isOn)
                        {
                            loadingStarData = false;
                        }
                        PlanetList.value = -1;
                        PlanetList.RefreshShownValue();
                    }
                }
            }
            //加载数据
            else if(UIRoot.instance.overlayCanvas.transform!=null && GameMain.instance!=null)
            {
                //加载UI
                ui_StarMapToolsBasePanel = GameObject.Instantiate(prefab_StarMapToolsBasePanel, UIRoot.instance.overlayCanvas.transform);
                ui_StarMapToolsBasePanel.transform.Find("TitleText").gameObject.AddComponent<Drag>();
                ui_StarMapToolsBasePanel.SetActive(false);
                //获取控件
                TitleText = ui_StarMapToolsBasePanel.transform.Find("TitleText").GetComponent<Text>();
                StarList =ui_StarMapToolsBasePanel.transform.Find("StarList").GetComponent<Dropdown>();
                PlanetList = ui_StarMapToolsBasePanel.transform.Find("PlanetList").GetComponent<Dropdown>();
                InfoText = ui_StarMapToolsBasePanel.transform.Find("InfoText").GetComponent<InputField>();
                LoadResAmount= ui_StarMapToolsBasePanel.transform.Find("LoadResAmount").GetComponent<Toggle>();
                OptionsList = ui_StarMapToolsBasePanel.transform.Find("OptionsList").GetComponent<ScrollRect>();
                SearchNextToggle= ui_StarMapToolsBasePanel.transform.Find("SearchNextToggle").GetComponent<Toggle>();
                DysonLuminoText = ui_StarMapToolsBasePanel.transform.Find("DysonLuminoText").GetComponent<InputField>();
                DistanceText = ui_StarMapToolsBasePanel.transform.Find("DistanceText").GetComponent<InputField>();
                ResultList = ui_StarMapToolsBasePanel.transform.Find("ResultList").GetComponent<Dropdown>();
                SearchButton = ui_StarMapToolsBasePanel.transform.Find("SearchButton").GetComponent<Button>();
                var TempToggle = OptionsList.content.Find("TempToggle").GetComponent<Toggle>();
                //翻译控件
                if (Localization.language != Language.zhCN)
                {
                    LoadResAmount.transform.Find("Label").GetComponent<Text>().text = "加载矿物数量".getTranslate();
                    SearchNextToggle.transform.Find("Label").GetComponent<Text>().text = "连续搜索".getTranslate();
                    SearchButton.transform.Find("Text").GetComponent<Text>().text = "搜索".getTranslate();
                    DysonLuminoText.transform.Find("Placeholder").GetComponent<Text>().text = "最小光度".getTranslate();
                    DistanceText.transform.Find("Placeholder").GetComponent<Text>().text = "最远距离".getTranslate();
                }
                //获取数据
                var TempStarTypes = starSearcher.AllStarTypes;
                var TempPlanteTypes=starSearcher.AllPlanteTypes;
                var TempSingularityTypes=starSearcher.AllSingularityTypes;
                var TempVeinTypes=starSearcher.AllVeinTypes;
                //各种选项的列表
                var StarTypesToggleList = new List<Toggle>();
                var PlanteTypesToggleList = new List<Toggle>();
                var SingularityTypesToggleList = new List<Toggle>();
                var VeinTypesToggleList = new List<Toggle>();
                //实例化
                for (int i = 0; i < TempStarTypes.Count;i++)
                {
                    var toggle = GameObject.Instantiate<Toggle>(TempToggle,TempToggle.transform.parent).GetComponent<RectTransform>();
                    toggle.Find("Label").GetComponent<Text>().text = TempStarTypes[i];
                    toggle.GetComponent<Toggle>().isOn = true;
                    toggle.anchorMax = new Vector2((float)0.25, (float)(1 - i * 0.1));
                    toggle.anchorMin = new Vector2(0, (float)(1 - (i+1) * 0.1));
                    toggle.gameObject.SetActive(true);
                    StarTypesToggleList.Add(toggle.GetComponent<Toggle>());
                }
                for (int i = 0; i < TempPlanteTypes.Count; i++)
                {
                    var toggle = GameObject.Instantiate<Toggle>(TempToggle, TempToggle.transform.parent).GetComponent<RectTransform>();
                    toggle.Find("Label").GetComponent<Text>().text = TempPlanteTypes[i];
                    toggle.anchorMax = new Vector2((float)0.5, (float)(1 - i * 0.1));
                    toggle.anchorMin = new Vector2((float)0.25, (float)(1 - (i + 1) * 0.1));
                    toggle.gameObject.SetActive(true);
                    PlanteTypesToggleList.Add(toggle.GetComponent<Toggle>());
                }
                for (int i = 0; i < TempSingularityTypes.Count; i++)
                {
                    var toggle = GameObject.Instantiate<Toggle>(TempToggle, TempToggle.transform.parent).GetComponent<RectTransform>();
                    toggle.Find("Label").GetComponent<Text>().text = TempSingularityTypes[i];
                    toggle.anchorMax = new Vector2((float)0.75, (float)(1 - i * 0.1));
                    toggle.anchorMin = new Vector2((float)0.5, (float)(1 - (i + 1) * 0.1));
                    toggle.gameObject.SetActive(true);
                    SingularityTypesToggleList.Add(toggle.GetComponent<Toggle>());
                }
                for (int i = 0; i < TempVeinTypes.Count; i++)
                {
                    var toggle = GameObject.Instantiate<Toggle>(TempToggle, TempToggle.transform.parent).GetComponent<RectTransform>();
                    toggle.Find("Label").GetComponent<Text>().text = TempVeinTypes[i];
                    toggle.anchorMax = new Vector2(1, (float)(1 - i * 0.1));
                    toggle.anchorMin = new Vector2((float)0.75, (float)(1 - (i + 1) * 0.1));
                    toggle.gameObject.SetActive(true);
                    VeinTypesToggleList.Add(toggle.GetComponent<Toggle>());
                }
                //切换恒星事件
                StarList.onValueChanged.AddListener(delegate {
                    PlanetList.ClearOptions();
                    if (StarList.value>=0 && StarList.value < galaxy.starCount)
                    {
                        var star = galaxy.StarById(StarList.value + 1);
                        if (LoadResAmount.isOn && UIRoot.instance.galaxySelect.starmap.galaxyData != null && !star.loaded)
                        {
                            star.Load();
                        }
                        PlanetList.options.Add(new Dropdown.OptionData("恒星".getTranslate()));
                        foreach (PlanetData planet in star.planets)
                        {
                            PlanetList.options.Add(new Dropdown.OptionData(planet.name));
                        }
                        PlanetList.value = -1;
                        PlanetList.RefreshShownValue();
                    }
                });
                //切换星球事件
                PlanetList.onValueChanged.AddListener(delegate {
                    var star = galaxy.StarById(StarList.value + 1);
                    if (PlanetList.value>0 && PlanetList.value <= star.planetCount)
                    {
                        var planet = star.planets[PlanetList.value - 1];
                        var info = planet.name+"的信息:".getTranslate()+ "\n";
                        info += "词条:".getTranslate() + planet.singularityString + "\n";//词条
                        info += "类型:".getTranslate() + planet.typeString + "\n";
                        string waterType = "未知".getTranslate();
                        switch (planet.waterItemId)
                        {
                            case -1:
                                waterType = "熔岩".getTranslate();
                                break;
                            case 0:
                                waterType = "无".getTranslate();
                                break;
                            default:
                                waterType = LDB.ItemName(planet.waterItemId);
                                break;
                        }
                        info += "海洋类型:".getTranslate() + waterType + "\n";
                        if(planet.type!= EPlanetType.Gas && planet.veinSpotsSketch!=null)
                        {
                            info += "矿物信息:".getTranslate() + "\n";
                            for(int i = 0; i <LDB.veins.Length; i++)
                            {
                                var name = LDB.veins.dataArray[i].name;
                                object amount = planet.veinAmounts[i + 1];
                                if (planet.veinSpotsSketch[i+1]==0)
                                {
                                    amount = "无".getTranslate();
                                }
                                else if ((long)amount == 0)
                                {
                                    if (!LoadResAmount.isOn)
                                    {
                                        amount = "有".getTranslate();
                                    }
                                    else if (UIRoot.instance.galaxySelect.starmap.galaxyData != null)
                                    {
                                        amount = "正在加载".getTranslate();
                                        loadingStarData = true;
                                    }
                                    else
                                    {
                                        amount = "未加载,靠近后显示".getTranslate();
                                    }
                                }
                                else if (i + 1 == 7)
                                {
                                    amount = (long)amount * (double)VeinData.oilSpeedMultiplier + " /s";
                                }
                                info += "    " + name + ":" + amount + "\n";
                            }
                        }
                        InfoText.text = info;
                    }
                    else if (PlanetList.value == 0)
                    {
                        var info = star.name + "星系的信息:".getTranslate() + (loadingStarData?"正在加载".getTranslate() : "")+"\n";
                        info += "恒星类型:".getTranslate() + star.typeString + "\n";
                        info += "星球数量:".getTranslate() + star.planetCount + "\n";
                        info += "光度:".getTranslate() + star.dysonLumino.ToString() + "\n";
                        info += "距离初始星系恒星:".getTranslate() + ((star.uPosition - galaxy.StarById(1).uPosition).magnitude / 2400000.0).ToString()+"光年".getTranslate()+ "\n";
                        info += "星球列表:".getTranslate();
                        foreach (PlanetData planet in star.planets)
                        {
                            info +="-"+planet.typeString + "  " + planet.singularityString;
                        }
                        info += "\n"+"矿物信息:".getTranslate() + "\n";
                        for (int i = 0; i < LDB.veins.Length; i++)
                        {
                            var name = LDB.veins.dataArray[i].name;
                            object amount = star.GetResourceAmount(i + 1);
                            if (star.GetResourceSpots(i + 1) == 0)
                            {
                                amount = "无".getTranslate();
                            }
                            else if ((long)amount == 0)
                            {
                                if (!LoadResAmount.isOn)
                                {
                                    amount = "有".getTranslate();
                                }
                                else if(UIRoot.instance.galaxySelect.starmap.galaxyData != null)
                                {
                                    amount = "正在加载".getTranslate();
                                    loadingStarData = true;
                                }
                                else
                                {
                                    amount = "未加载,靠近后显示".getTranslate();
                                }
                            }
                            else if (i + 1 == 7)
                            {
                                amount = (long)amount * (double)VeinData.oilSpeedMultiplier + " /s";
                            }
                            info += "    " + name + ":" + amount + "\n";
                        }
                        InfoText.text = info;
                    }
                });
                //搜索事件
                SearchButton.onClick.AddListener(delegate {
                    SearchNext = false;
                    starSearcher.galaxyData = galaxy;
                    starSearcher.Clear();
                    float.TryParse(DysonLuminoText.text == "" ? "0":DysonLuminoText.text, out starSearcher.dysonLumino);
                    float.TryParse(DistanceText.text==""?"1000": DistanceText.text, out starSearcher.distance);
                    foreach (Toggle toggle in StarTypesToggleList)
                    {
                        if (toggle.isOn)
                        {
                            var typeString = toggle.transform.Find("Label").GetComponent<Text>().text;
                            starSearcher.StarTypes.Add(typeString);
                        }
                    }
                    foreach (Toggle toggle in PlanteTypesToggleList)
                    {
                        if (toggle.isOn)
                        {
                            var typeString = toggle.transform.Find("Label").GetComponent<Text>().text;
                            starSearcher.PlanteTypes.Add(typeString);
                        }
                    }
                    foreach (Toggle toggle in SingularityTypesToggleList)
                    {
                        if (toggle.isOn)
                        {
                            var typeString = toggle.transform.Find("Label").GetComponent<Text>().text;
                            starSearcher.SingularityTypes.Add(typeString);
                        }
                    }
                    foreach (Toggle toggle in VeinTypesToggleList)
                    {
                        if (toggle.isOn)
                        {
                            var typeString = toggle.transform.Find("Label").GetComponent<Text>().text;
                            starSearcher.VeinTypes.Add(typeString);
                        }
                    }
                    SerachResult = starSearcher.Search();
                    ResultList.ClearOptions();
                    foreach (StarData star in SerachResult)
                    {
                        ResultList.options.Add(new Dropdown.OptionData(star.name));
                    }
                    ResultList.value = -1;
                    ResultList.RefreshShownValue();
                    if (SerachResult.Count == 0 && SearchNextToggle.isOn)
                    {
                        SearchNext = true;
                    }
                });
                //切换搜索结果事件
                ResultList.onValueChanged.AddListener(delegate {
                    StarList.value = SerachResult[ResultList.value].index;
                    StarList.RefreshShownValue();
                });
                dataLoadOver = true;
            }
        }
        IEnumerator wait(object target)
        {
            yield return new WaitForEndOfFrame();//等待帧结束
            //传送
            if (target is PlanetData)
            {
                GameMain.mainPlayer.uPosition =((PlanetData)target).uPosition + VectorLF3.unit_z * (((PlanetData)target).realRadius);
            }
            else if(target is StarData)
            {
                GameMain.mainPlayer.uPosition = ((StarData)target).uPosition + VectorLF3.unit_z * (((StarData)target).physicsRadius);
                loadingStarData = true;
            }else if(target is VectorLF3)
            {
                GameMain.mainPlayer.uPosition = (VectorLF3)target;
            }
            else if(target is string && (string)target == "resize")
            {
                GameMain.mainPlayer.transform.localScale = Vector3.one;
            }
            if (!(target is string) || (string)target != "resize")
            {
                StartCoroutine(wait("resize"));
            }
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIStarmap), "OnStarClick")]
        private static bool OnStarClick(UIStarmapStar star)
        {
            if (self.showGUI)
            {
                self.StarList.value = star.star.index;
                self.StarList.RefreshShownValue();
            }
            return true;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIStarmap), "OnPlanetClick")]
        private static bool OnPlanetClick(UIStarmapPlanet planet)
        {
            if (self.showGUI)
            {
                self.StarList.value = planet.planet.star.index;
                self.StarList.RefreshShownValue();
                self.PlanetList.value = planet.planet.index + 1;
                self.PlanetList.RefreshShownValue();
            }
            return true;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GalaxyData), "Free")]
        private static bool Free(GalaxyData __instance)
        {
            foreach(StarData star in __instance.stars)
            {
                foreach(PlanetData planet in star.planets)
                {
                    if (planet.loading)
                    {
                        Debug.Log("由StarMapTools阻止的GalaxyData.Free()");
                        return false;
                    }
                }
            }
            return true;
        }
    }
    class Drag : MonoBehaviour
    {
        RectTransform rt;
        RectTransform parent;
        RectTransform canvas;
        Vector3 lastPosition;
        bool drag = false;
        void Start()
        {
            rt = GetComponent<RectTransform>();//标题栏的rt
            parent = rt.parent.GetComponent<RectTransform>();//BasePanel的rt
            canvas = parent.parent.GetComponent<RectTransform>();//canvas的rt
        }
        void Update()
        {
            //获取鼠标在游戏窗口的unity坐标
            var m = Input.mousePosition - Vector3.right * Screen.width / 2 - Vector3.up * Screen.height / 2;
            m.x *= canvas.sizeDelta.x / Screen.width;
            m.y *= canvas.sizeDelta.y / Screen.height;
            //获取标题在游戏窗口内的坐标
            var rp = parent.localPosition + rt.localPosition;
            //获取标题的rect
            var re = rt.rect;
            //判断鼠标是否在标题的范围内按下
            if (m.x >= rp.x - re.width / 2 && m.x <= rp.x + re.width / 2 && m.y >= rp.y - re.height / 2 && m.y <= rp.y + re.height / 2 && Input.GetMouseButtonDown(0))
            {
                drag = true;
                lastPosition = m;
            }
            //获取鼠标是否松开
            else if (drag && Input.GetMouseButtonUp(0))
            {
                drag = false;
            }
            //根据鼠标的拖动更新窗口位置
            if (drag)
            {
                parent.localPosition += m - lastPosition;
                lastPosition = m;
            }
        }
    }
    //恒星搜索器
    class StarSearcher
    {
        public GalaxyData galaxyData { get; set; }
        public List<string> StarTypes = new List<string>();//搜索的恒星类型属于其中之一
        public List<string> PlanteTypes = new List<string>();//搜索的星系中包含所有以下类型星球
        public List<string> SingularityTypes = new List<string>();//搜索的星系包含所有以下的词条
        public List<string> VeinTypes = new List<string>();//搜索的星系包含以下所有的矿物
        public float dysonLumino;//搜索的恒星的光度需大于该值
        public float distance;//搜索的恒星距离初始星系的距离需小于此值(单位光年)
        public List<string> AllStarTypes
        {
            get
            {
                var list = new List<string>();
                list.Add("红巨星".Translate());
                list.Add("黄巨星".Translate());
                list.Add("白巨星".Translate());
                list.Add("蓝巨星".Translate());

                list.Add("M" + "型恒星".Translate());
                list.Add("K" + "型恒星".Translate());
                list.Add("G" + "型恒星".Translate());
                list.Add("F" + "型恒星".Translate());
                list.Add("A" + "型恒星".Translate());
                list.Add("B" + "型恒星".Translate());
                list.Add("O" + "型恒星".Translate());

                list.Add("中子星".Translate());
                list.Add("白矮星".Translate());
                list.Add("黑洞".Translate());
                return list;
            }
        }
        public List<string> AllPlanteTypes
        {
            get
            {
                var list = new List<string>();
                foreach (ThemeProto themeProto in LDB.themes.dataArray)
                {
                    if (!list.Contains(themeProto.displayName))
                    {
                        list.Add(themeProto.displayName);
                    }
                }
                return list;
            }
        }
        public List<string> AllSingularityTypes {
            get
            {
                var list = new List<string>();
                list.Add("卫星".Translate());
                list.Add("潮汐锁定永昼永夜".Translate());
                list.Add("潮汐锁定1:2".Translate());
                list.Add("潮汐锁定1:4".Translate());
                list.Add("横躺自转".Translate());
                list.Add("反向自转".Translate());
                list.Add("多卫星".Translate());
                return list;
            }
        }
        public List<string> AllVeinTypes
        {
            get
            {
                var list = new List<string>();
                foreach(VeinProto veinProto in LDB.veins.dataArray)
                {
                    list.Add(veinProto.name);
                }
                return list;
            }
        }
        //查找星系
        public List<StarData> Search()
        {
            List<StarData> result = new List<StarData>();
            if (galaxyData != null)
            {
                foreach(StarData star in galaxyData.stars)
                {
                    if (StarTypes.Contains(star.typeString) && star.dysonLumino>=dysonLumino && ((star.uPosition - galaxyData.StarById(1).uPosition).magnitude / 2400000.0)<=distance)
                    {
                        List<string> TempPlanteTypes = new List<string>();
                        List<string> TempSingularityTypes = new List<string>();
                        List<string> TempVeinTypes = new List<string>();
                        foreach(PlanetData planet in star.planets)
                        {
                            if (!TempPlanteTypes.Contains(planet.typeString))
                            {
                                TempPlanteTypes.Add(planet.typeString);
                            }
                            if (!TempSingularityTypes.Contains(planet.singularityString))
                            {
                                TempSingularityTypes.Add(planet.singularityString);
                            }
                        }
                        for(int i = 0; i < LDB.veins.Length; i++)
                        {
                            if (star.GetResourceSpots(i + 1) > 0)
                            {
                                TempVeinTypes.Add(LDB.veins.dataArray[i].name);
                            }
                        }
                        if (PlanteTypes.TrueForAll(delegate (string ePlanetType) { return TempPlanteTypes.Contains(ePlanetType); }) && SingularityTypes.TrueForAll(delegate (string ePlanetSingularity) { return TempSingularityTypes.Contains(ePlanetSingularity); }) && VeinTypes.TrueForAll(delegate (string eVeinType) { return TempVeinTypes.Contains(eVeinType); }))
                        {
                            result.Add(star);
                        }
                    }
                }
                return result;
            }
            else
            {
                return result;
            }
        }
        public void Clear()
        {
            StarTypes.Clear();
            PlanteTypes.Clear();
            SingularityTypes.Clear();
            VeinTypes.Clear();
            dysonLumino = 0;
            distance = 1000;
        }
    }

    //翻译器
    public static class StarMapToolsTranslate
    {
        static Dictionary<string, string> TranslateDict = new Dictionary<string, string>();
        public static string getTranslate(this string s)
        {
            if (Localization.language!=Language.zhCN && TranslateDict.ContainsKey(s))
            {
                return TranslateDict[s];
            }
            else
            {
                return s;
            }
        }
        public static void regAllTranslate()
        {
            TranslateDict.Clear();
            TranslateDict.Add("开关GUI的按键", "Open and Close GUI Key");
            TranslateDict.Add("传送按键", "TP Key");
            TranslateDict.Add("新游戏模式", "NEW GAME");
            TranslateDict.Add("读档模式", "IN GAME");
            TranslateDict.Add("等待数据", "WAIT DATA");
            TranslateDict.Add("加载矿物数量", "Load Vein Amount");
            TranslateDict.Add("连续搜索", "Search Next Seed");
            TranslateDict.Add("最小光度", "Minimum luminosity");
            TranslateDict.Add("最远距离", "Maximum light year");
            TranslateDict.Add("搜索", "Search");

            TranslateDict.Add("恒星", "Star");
            TranslateDict.Add("恒星类型:", "Star Type:");
            TranslateDict.Add("星球数量:", "Planet Num:");
            TranslateDict.Add("星系的信息:", " Info:");
            TranslateDict.Add("光度:", "Luminosity:");
            TranslateDict.Add("距离初始星系恒星:", "Distance from the beginning:");
            TranslateDict.Add("光年", "Light years");
            TranslateDict.Add("星球列表:", "Planet List:");

            TranslateDict.Add("的信息:", " Info:");
            TranslateDict.Add("类型:", "Type:");
            TranslateDict.Add("词条:", "Singularity:");
            TranslateDict.Add("海洋类型:", "Ocean Type:");
            TranslateDict.Add("未知", " Unknown:");
            TranslateDict.Add("熔岩", " Lava");
            TranslateDict.Add("矿物信息:", "VeinList:");

            TranslateDict.Add("无", "None");
            TranslateDict.Add("有", "Exist");
            TranslateDict.Add("正在加载", "Loading");
            TranslateDict.Add("未加载,靠近后显示", "Not loaded, display when approaching");
        }
    }
}
