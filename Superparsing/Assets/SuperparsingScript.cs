using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;

public class SuperparsingScript : MonoBehaviour {

    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMBombModule Module;

    public KMSelectable displayBtn;
    public KMSelectable[] quadrants;
    public KMSelectable[] switches;
    public KMSelectable[] slidersLeft;
    public KMSelectable[] slidersRight;
    public KMSelectable sliderSubmit;
    public KMSelectable dial;
    public KMSelectable dialSubmit;
    public GameObject modBG;
    public GameObject meter;
    public TextMesh displayText;
    public MeshRenderer[] highlights;
    public Material highlightcol;
    private float timeLimit, shakeFactor;
    private float timeLerp = 0f;
    public GameObject[] sliderKnobs;
    public GameObject[] switchHandles;
    public GameObject dialParent;

    public GameObject[] squares;
    private Vector3[] squareTransforms = new Vector3[4];
    int[] squarePositions = Enumerable.Range(0, 4).ToArray();
    private readonly string[] romanNumerals = new[] { "I", "II", "III", "IV" };
    private Coroutine heatUp;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    private bool started;
    bool coolingDown;
    bool[] stagesSolved = new bool[4];

    string displayedWord;
    private readonly string[] quadrantPositions = new[] { "top-left", "top-right", "bottom-left", "bottom-right" };
    private readonly string[] sliderPositions = new[] { "Left", "Middle", "Right" };
    private readonly string[] dialPositions = new[] { "North", "East", "South", "West" };
    int correctQuadrant = -1;
    bool[] currentSwitches = new bool[2] { true, true };
    bool[] correctSwitches = new bool[2];
    bool[] switchesMoving = new bool[2];
    int prevSwitch = -1;
    int[] currentSliders = new int[3];
    int[] correctSliders = new int[3];
    Coroutine[] sliderMovements = new Coroutine[3];
    bool dialSpinning;
    int dialPos;
    int[] correctDialPositions;
    bool dialFlipped;
    bool TwitchPlaysActive;

    class SuperparsingSettings
    {
        public float timer = 10;
        public float shakeF = 1;
    }
    SuperparsingSettings settings = new SuperparsingSettings();
    private static Dictionary<string, object>[] TweaksEditorSettings = new Dictionary<string, object>[]
    {
          new Dictionary<string, object>
          {
            { "Filename", "SuperparsingSettings.json"},
            { "Name", "Superparsing" },
            { "Listings", new List<Dictionary<string, object>>
                {
                  new Dictionary<string, object>
                  {
                    { "Key", "timer" },
                    { "Text", "Time limit of the module."}
                  },
                  new Dictionary<string, object>
                  {
                    { "Key", "shakeF" },
                    { "Text", "Just for fun, alters how much the mod shakes when heating up."}
                  }

                }
            }
          }
    };

    void Awake ()
    {
        moduleId = moduleIdCounter++;
        foreach (KMSelectable quadrant in quadrants)
        {
            quadrant.OnInteract += delegate () { QuadrantPress(Array.IndexOf(quadrants, quadrant)); return false; };
            quadrant.OnHighlight += delegate () { if (!stagesSolved[0]) quadrant.GetComponent<MeshRenderer>().material.color = "E16C6C".Color(); };
            quadrant.OnHighlightEnded += delegate () { if (!stagesSolved[0]) quadrant.GetComponent<MeshRenderer>().material.color = "784B4B".Color(); };
        }
        foreach (KMSelectable lever in switches)
            lever.OnInteract += delegate () { SwitchFlip(Array.IndexOf(switches, lever)); return false; };
        foreach (KMSelectable leftSlider in slidersLeft)
            leftSlider.OnInteract += delegate () { MoveSlider(Array.IndexOf(slidersLeft, leftSlider), -1); return false; };
        foreach (KMSelectable rightSlider in slidersRight)
            rightSlider.OnInteract += delegate () { MoveSlider(Array.IndexOf(slidersRight, rightSlider), 1); return false; };
        dial.OnInteract += delegate () { DialPress(); return false; };
        sliderSubmit.OnInteract += delegate () { SubmitSliders(); return false; };
        dialSubmit.OnInteract += delegate () { SubmitDial(); return false; };
        displayBtn.OnInteract += delegate () { StartTimer(); return false; };
        for (int i = 0; i < 4; i++)//Caches the initial position of each square.
            squareTransforms[i] = squares[i].transform.localPosition;
        ModConfig<SuperparsingSettings> config = new ModConfig<SuperparsingSettings>("SuperparsingSettings");
        settings = config.Read();
        config.Write(settings);
        timeLimit = settings.timer <= 0 ? 10 : settings.timer; //Time limit cannot be negative or zero
        shakeFactor = settings.shakeF;
        Module.OnActivate += delegate () 
        {
            Audio.PlaySoundAtTransform("startup", transform); 
            if (TwitchPlaysActive) 
                timeLimit = 20; 
        };
    }

    void Start ()
    {
        displayText.text = string.Empty;
        RandomizeData();
    }
    void RandomizeData()
    {
        squarePositions.Shuffle(); //Puts the squares in a random order.
        for (int sqIx = 0; sqIx < 4; sqIx++)
            squares[squarePositions[sqIx]].transform.localPosition = squareTransforms[sqIx]; //Moves each square to the corresponding place in its order.
        Debug.LogFormat("[Superparsing #{0}] The squares in reading order are: {1}.", moduleId, squarePositions.Select(x => romanNumerals[x]).Join(", "));
        for (int swIx = 0; swIx < 2; swIx++)
            if (Rnd.Range(0, 2) == 0)
            {
                switchHandles[swIx].transform.localEulerAngles = new Vector3(-70, 0, 0); //Flips the switch position;
                currentSwitches[swIx] = false;
            }
        dialPos = Rnd.Range(0, 4);
        dial.transform.localEulerAngles = new Vector3(0, 0, 90) * dialPos;
        for (int sdIx = 0; sdIx < 3; sdIx++)
        {
            int move = Rnd.Range(-1, 2);//-1, 0, or 1;
            currentSliders[sdIx] = move;
            sliderKnobs[sdIx].transform.localPosition += new Vector3(0.25f, 0, 0) * move; //Moves the slider left if move == -1, right if move == +1, and does nothing if move == 0.
        }
        if (Rnd.Range(0,2) == 0) //Controls whether the dial is to the right or left of the submit button.
        {
            dialFlipped = true;
            dialParent.transform.localPosition += 0.3f * Vector3.right; //The dial is stored under a separate gameobject, so we need to dig into the parent
            dialSubmit.transform.localPosition += 0.6f * Vector3.left;
        }
    }

    #region Timer Methods
    void Update () //Ran once every frame; updates the bar and the background color.
    {
        meter.SetActive(timeLerp > 0);
        meter.transform.localScale = new Vector3(1, 1, timeLerp);
        modBG.GetComponent<MeshRenderer>().material.color = Color.Lerp("2D1C1C".Color(), "FF2020".Color(), Mathf.Pow(timeLerp, 3));
    }
    void FixedUpdate() //Is ran 50 times per second, no matter the framerate.
    {
        //Sets the position of the background to a random value. The range of possible values increases as the time increases. Increases exponentially through Mathf.Pow
        modBG.transform.localPosition = shakeFactor * Mathf.Pow(timeLerp, 6) * new Vector3(Rnd.Range(-0.025f, 0.025f), 0, Rnd.Range(-0.025f, 0.025f));
    }
    IEnumerator HeatUp()
    {
        while (true)
        {
            while (timeLerp < 1)
            {
                if (started && !coolingDown) //Timer does not increase when the module is inactive or when it is cooling down.
                    timeLerp += 1f / timeLimit * Time.deltaTime; //Increases timer.
                yield return null;
            }
            if (timeLerp >= 1) //When the timer runs out.
            {
                Audio.PlaySoundAtTransform("kapow", transform);
                GetComponent<KMSelectable>().AddInteractionPunch(50);
                Debug.LogFormat("[Superparsing #{0}] Timer expired, strike incurred.", moduleId);
                timeLerp = 0;
                Strike();
            }
            yield return null;
        }


    }
    IEnumerator ReduceTimer(float threshold, float speed = 0.4f)
    { //Threshold represents the point at which the bar stops lowering.
        while (timeLerp > threshold)
        {
            timeLerp -= Time.deltaTime * speed;
            yield return null;
        }
        timeLerp = threshold;
        coolingDown = false;
    }
    #endregion
    #region OnInteract Handlers
    void QuadrantPress(int pos)
    {
        if (stagesSolved[0])
            return;
        Audio.PlaySoundAtTransform("quadrantpress", quadrants[pos].transform);
        quadrants[pos].AddInteractionPunch(0.3f);
        if (!started)
            return;
        if (pos == correctQuadrant)
        {
            Debug.LogFormat("[Superparsing #{0}] You pressed the {1} quadrant, square disarmed.", moduleId, quadrantPositions[pos]);
            Solve(0);
            StartCoroutine(QuadrantsFade(Color.black));
        }
        else
        {
            Debug.LogFormat("[Superparsing #{0}] You pressed the {1} quadrant, strike.", moduleId, quadrantPositions[pos]);
            Strike();
        }
    }
    IEnumerator QuadrantsFade(Color target) 
    {//Lerps from the current square color to a target color.
        float delta = 0;
        while (delta < 1)
        {
            yield return null;
            delta += Time.deltaTime;
            for (int i = 0; i < 4; i++)
            {
                Color curCol = quadrants[i].GetComponent<MeshRenderer>().material.color;
                quadrants[i].GetComponent<MeshRenderer>().material.color = Color.Lerp(curCol, target, delta);
            }
        }
    }
    void SwitchFlip(int pos)
    {
        if (switchesMoving[pos])
            return;
        currentSwitches[pos] = !currentSwitches[pos]; //Toggles the switch value.
        Audio.PlaySoundAtTransform("lever", switches[pos].transform);
        StartCoroutine(MoveSwitch(pos));
        if (!started || stagesSolved[1]) //If the module hasn't started yet, don't worry about checking.
            return;
        if (prevSwitch == pos) //If you flip the same switch twice in a row, submit.
            StartCoroutine(SwitchSubmit());
        prevSwitch = pos;
    }
    IEnumerator SwitchSubmit()
    {
        yield return new WaitForSeconds(0.3f);
        if (started) //Since there's a delay, this prevents switches from solving when the module is deactivated.
        {
            if (currentSwitches[0] == correctSwitches[0] && currentSwitches[1] == correctSwitches[1])
            {
                Debug.LogFormat("[Superparsing #{0}] Switches submitted with positions {1}. Square disarmed.", moduleId, currentSwitches.Select(x => x ? "Up" : "Down").Join(", "));
                Solve(1);
            }
            else
            {
                Debug.LogFormat("[Superparsing #{0}] Switches submitted with positions {1}. Strike.", moduleId, currentSwitches.Select(x => x ? "Up" : "Down").Join(", "));
                Strike();
            }
        }
    }
    IEnumerator MoveSwitch(int pos)
    {
        switchesMoving[pos] = true;
        Transform TF = switchHandles[pos].transform;
        float from = currentSwitches[pos] ? -70 : 70;
        float to = -from;
        float startTime = Time.fixedTime; //Code taken from Colored Switches
        do
        {
            switchHandles[pos].transform.localEulerAngles = new Vector3(Easing.OutSine(Time.fixedTime - startTime, from, to, 0.5f), 0, 0);
            yield return null;
        }
        while (Time.fixedTime < startTime + 0.5f);
        switchHandles[pos].transform.localEulerAngles = new Vector3(to, 0, 0);
        switchesMoving[pos] = false;
    }
    void MoveSlider(int pos, int direction)
    {
        if (currentSliders[pos] + direction < -1 || currentSliders[pos] + direction > 1) //If you are trying to move out of bounds, abort.
            return;
        Audio.PlaySoundAtTransform("slide", sliderKnobs[pos].transform);
        currentSliders[pos] += direction;
        if (sliderMovements[pos] != null)
            StopCoroutine(sliderMovements[pos]);
        sliderMovements[pos] = StartCoroutine(SliderAnim(pos));
    }
    IEnumerator SliderAnim(int pos)
    {
        float from = sliderKnobs[pos].transform.localPosition.x;
        float to = 0.25f * currentSliders[pos];
        float startTime = Time.fixedTime;
        float duration = 0.5f;
        do
        {
            sliderKnobs[pos].transform.localPosition = new Vector3(Easing.OutQuad(Time.fixedTime - startTime, from, to, duration), 0.53f, 0);
            yield return null;
        }
        while (Time.fixedTime < startTime + duration);
        sliderKnobs[pos].transform.localPosition = new Vector3(to, 0.53f, 0);
    }
    void SubmitSliders()
    {
        sliderSubmit.AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, sliderSubmit.transform);
        if (stagesSolved[2] || !started)
            return;
        if (currentSliders.SequenceEqual(correctSliders)) //If each slider is in the correct position.
        {
            Debug.LogFormat("[Superparsing #{0}] Sliders submitted in positions {1}. Square disarmed.", moduleId, currentSliders.Select(x => sliderPositions[x + 1]).Join(", "));
            Solve(2);
        }
        else
        {
            Debug.LogFormat("[Superparsing #{0}] Sliders submitted in positions {1}. Strike.", moduleId, currentSliders.Select(x => sliderPositions[x + 1]).Join(", "));
            Strike();
        }
    }
    void DialPress()
    {
        dialPos++;
        dialPos %= 4;
        Audio.PlaySoundAtTransform("knob", dial.transform);
        StartCoroutine(DialSpin());
    }
    IEnumerator DialSpin()
    {
        yield return new WaitUntil(() => !dialSpinning); //Causes a stacking effect. If multiple instances of this are ran, they will each progress one-by-one. In a way, it uses a queue system, although without the collection.
        dialSpinning = true;
        Vector3 current = dial.transform.localEulerAngles;
        Vector3 target = current + 90 * Vector3.forward;
        float delta = 0;
        while (delta < 1)
        {
            delta += 5 * Time.deltaTime;
            yield return null;
            dial.transform.localEulerAngles = Vector3.Lerp(current, target, delta);
        }
        dialSpinning = false;

    }
    void SubmitDial()
    {
        dialSubmit.AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, dialSubmit.transform);
        if (stagesSolved[3] || !started)
            return;
        if (correctDialPositions.Contains(dialPos))
        {
            Debug.LogFormat("[Superparsing #{0}] Dial submitted at position {1}. Square disarmed.", moduleId, dialPositions[dialPos]);
            Solve(3);
        }
        else
        {
            Debug.LogFormat("[Superparsing #{0}] Dial submitted at position {1}. Strike.", moduleId, dialPositions[dialPos]);
            Strike();
        }
    }
    void StartTimer()
    {
        displayBtn.AddInteractionPunch(2.5f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, displayBtn.transform);
        if (started || moduleSolved)
            return;
        started = true;
        StartCoroutine(Begin());

    }
    #endregion

    void Solve(int pos) //Indicates solving one of the squares. pos represents the solved quadrant. 
    {
        coolingDown = true;
        stagesSolved[pos] = true;
        StartCoroutine(ShowHighlight(pos, false));
        if (stagesSolved.All(x => x))
            StartCoroutine(SolveModule());
        else StartCoroutine(ReduceTimer(timeLerp / (0.4f * timeLimit))); //Accounts for any time gained while the bar reduces, so that the timer is *actually* 10 seconds.
        Audio.PlaySoundAtTransform("shimmer", transform);
    }
    IEnumerator ShowHighlight(int pos, bool reverse) //Turns the border white. if reverse is true, it'll play the animation backwards.
    {
        float delta = 0;
        while (delta < 1)
        {
            delta += Time.deltaTime;
            highlights[pos].material.SetColor("_OutlineColor", new Color(1, 0.95f, 0.95f, reverse ? 1 - delta : delta));
            yield return null;
        }
    }

    IEnumerator SolveModule()
    {
        moduleSolved = true;
        Debug.LogFormat("[Superparsing #{0}] ALL SQUARES SOLVED // MODULE SHUTTING DOWN // THANK YOU FOR PLAYING.", moduleId);
        started = false;
        displayText.text = string.Empty;
        for (int i = 0; i < 4; i++)
            StartCoroutine(ShowHighlight(i, true)); //Fades out all the borders.
        StartCoroutine(ReduceTimer(0, 0.15f)); //Slowly cools down the module.
        yield return new WaitUntil(() => timeLerp <= 0); //Waits until the module has fully cooled before calling HandlePass;
        Audio.PlaySoundAtTransform("solve", transform);
        Module.HandlePass();
    }

    void Strike()
    {
        started = false;
        for (int i = 0; i < 4; i++)
        {
            if (stagesSolved[i])
                StartCoroutine(ShowHighlight(i, true)); //Disables each border that is already white.
            stagesSolved[i] = false;
            StartCoroutine(QuadrantsFade(new Color32(0x78, 0x4b, 0x4B, 0xFF))); //Sets the quadrants submod back to their default color.
        }
        displayText.text = string.Empty;
        prevSwitch = -1; //Resets the previously flipped switch.
        Module.HandleStrike();
        StartCoroutine(ReduceTimer(0));
    }
    IEnumerator Begin()
    {
        yield return new WaitForSeconds(1);
        displayedWord = WordList.words.Where(word => word.Any(ch => GetDialString().Contains(ch))).PickRandom(); //Guarantees that there's a valid dial answer.
        displayText.text = displayedWord;
        Debug.LogFormat("[Superparsing #{0}] The displayed word is {1}. Let the games begin.", moduleId, displayedWord);
        CalculateQuadrants();
        CalculateSwitches(); 
        CalculateSliders();
        CalculateDial();
        yield return new WaitForSeconds(1); //Gives one second between displaying the word and starting the timer.
        if (heatUp == null)
            heatUp = StartCoroutine(HeatUp());
    }
    #region Module Calculations
    void CalculateQuadrants()
    {
        Debug.LogFormat("[Superparsing #{0}] ::QUADRANTS::", moduleId);
        int ix = int.Parse(Bomb.GetSerialNumberNumbers().Join("")) % 4;
        int[] sortedOrder = Enumerable.Range(0, 4).OrderBy(x => displayedWord[x]).ToArray(); //Orders the quadrants by their corresponding letters.
        correctQuadrant = sortedOrder[ix]; //Indexes into the order by the SN %4 to obtain the correct answer.
        Debug.LogFormat("[Superparsing #{0}] The order generated from the word is {1}. Indexing into this by the serial mod 4 yields {2}.", moduleId, sortedOrder.Select(x => x + 1).Join(), correctQuadrant + 1);
        Debug.LogFormat("[Superparsing #{0}] You should press the {1} quadrant.", moduleId, quadrantPositions[correctQuadrant]);
    }
    void CalculateSwitches()
    {
        Debug.LogFormat("[Superparsing #{0}] ::SWITCHES::", moduleId);
        Func<char, bool> condition; //Returns a value for each letter of the displayed word.
        string loggingString = "For the letter to represent a 1, the letter must ";
        switch (Bomb.GetOnIndicators().Count() - Bomb.GetOffIndicators().Count())
        {
            case -2: loggingString += string.Format("be before {0} in the alphabet.", Bomb.GetSerialNumber()[3]); condition = x => x < Bomb.GetSerialNumber()[3]; break;
            case -1: loggingString += "be within the range A-M inclusive."; condition = x => x - 'A' < 13; break;
            case 0: loggingString += "be a vowel."; condition = x => "AEIOU".Contains(x); break;
            case 1: loggingString += "be within the range N-Z inclusive."; condition = x => x - 'A' >= 13; break;
            case 2: loggingString += string.Format("be later than {0} in the alphabet.", Bomb.GetSerialNumber()[4]); condition = x => x > Bomb.GetSerialNumber()[4]; break;
            default: loggingString += "be a consonant."; condition = x => !"AEIOU".Contains(x); break;
        }
        bool[] binary = displayedWord.Select(x => condition(x)).ToArray(); //Plugs each letter into the condition.
        Debug.LogFormat("[Superparsing #{0}] {1}", moduleId, loggingString);
        Debug.LogFormat("[Superparsing #{0}] The generated truth values from the word is {1}.", moduleId, binary.Select(x => x ? 'T' : 'F').Join(""));
        for (int i = 0; i < 2; i++) //Obtains the correct switch position for each pair.
        {
            bool b1 = binary[2 * i];
            bool b2 = binary[2 * i + 1];
            switch (Bomb.GetPortCount() % 6)
            {
                case 0: correctSwitches[i] = b1 & b2; break;
                case 1: correctSwitches[i] = b1 | b2; break;
                case 2: correctSwitches[i] = b1 ^ b2; break;
                case 3: correctSwitches[i] = !(b1 & b2); break;
                case 4: correctSwitches[i] = !(b1 | b2); break;
                case 5: correctSwitches[i] = !(b1 ^ b2); break;
            }
        }
        string[] gates = new[] { "n AND", "n OR", "n XOR", " NAND", " NOR", "n XNOR" };
        Debug.LogFormat("[Superparsing #{0}] We are applying a{1} gate. The correct switch positions are {2}.",
            moduleId, gates[Bomb.GetPortCount() % 6], correctSwitches.Select(x => x ? "Up" : "Down").Join(", "));
    }
    void CalculateSliders()
    {
        Debug.LogFormat("[Superparsing #{0}] ::SLIDERS::", moduleId);
        string[] wordTable = new string[]
        {
            "ELSE", "ZITI", "AREA", "POUR", "KILL", "TRUE",
            "BUNK", "OWES", "DINO", "SPAR", "DARE", "QUIP",
            "YOUR", "FLIP", "ATOM", "LAUD", "URGE", "JINX",
            "NODE", "CLUE", "HULA", "WORN", "RAIL", "COUP",
            "GROW", "XYLO", "MILE", "BREW", "ICON", "VIEW"
        };
        int row;
        int col;
        if (Bomb.GetBatteryCount() <= 1) row = 0;
        else if (Bomb.GetBatteryCount() >= 4) row = 2;
        else row = 1;
        if (Bomb.GetPortCount() >= 4) col = 4;
        else col = Bomb.GetPortCount();
        int tlcorner = 6 * row + col;
        string[] tableSection = new int[] { 0, 1, 6, 7, 12, 13 }.Select(x => wordTable[x + tlcorner]).ToArray();
        for (int i = 0; i < 3; i++)
            correctSliders[i] = GetSliderPos(tableSection[2 * i], tableSection[2 * i + 1], displayedWord);
        Debug.LogFormat("[Superparsing #{0}] The table starts with the {1} cell in reading order, giving the words: {2}.", moduleId, Ordinal(tlcorner + 1), tableSection.Join(", "));
        Debug.LogFormat("[Superparsing #{0}] The correct sliders positions are {1}.", moduleId, correctSliders.Select(x => sliderPositions[x + 1]).Join(", "));
    }
    int GetSliderPos(string s1, string s2, string word)
    {
        if ((StringGreater(s1, word) && StringGreater(s2, s1)) || (StringGreater(word, s1) && StringGreater(s1, s2)))
            return -1;
        if ((StringGreater(word, s1) && StringGreater(s2, word)) || (StringGreater(s1, word) && StringGreater(word, s2)))
            return 0;
        if ((StringGreater(s2, s1) && StringGreater(word, s2)) || (StringGreater(s1, s2) && StringGreater(s2, word)))
            return 1;
        throw new ArgumentException("Word cannot be fitted... somehow");
    }
    static bool StringGreater(string s1, string s2)
    {
        for (int i = 0; i < 4; i++)
        {
            if (s1[i] != s2[i])
                return s1[i] > s2[i];
        }
        return false;
    }
    #endregion

    void CalculateDial()
    {
        correctDialPositions = Enumerable.Range(0, 4).Where(x => displayedWord.Contains(GetDialString()[x])).ToArray();
        Debug.LogFormat("[Superparsing #{0}] The spaces around the dial correspond to the letters {1}. You should turn the dial to one of the following: {2}.",
            moduleId, GetDialString(), correctDialPositions.Select(x => dialPositions[x]).Join(", "));
        if (dialFlipped)
        {
            correctDialPositions = correctDialPositions.Select(x => (x + 2) % 4).ToArray();
            Debug.LogFormat("[Superparsing #{0}] Because the dial is on the right, you should actually turn the dial to one of: {1}.", moduleId, correctDialPositions.Select(x => dialPositions[x]).Join(", "));
        }
    }
    string GetDialString()
    {
        string[] squareLocations = new[] { "0123", "0132", "0213", "0231", "0312", "0321", "1023", "1032", "1203", "1230", "1302", "1320", "2013", "2031", "2103", "2130", "2301", "2310", "3012", "3021", "3102", "3120", "3201", "3210", };
        string[] letters = new[] { "-TOE", "-ANT", "-ISA", "--RI", "EOH-", "TNDO", "ASLN", "IRUS", "ODCH", "NLMD", "SUFL", "R-YU", "HCW-", "DMGC", "LFPM", "UYBF", "CGVW", "MPKG", "FBXP", "Y-QB", "WV--", "GK-V", "PX-K", "BQ-X", };
        return letters[Array.IndexOf(squareLocations, squarePositions.Join(""))];
    }
    static string Ordinal(int num)
    {
        switch (num % 100)
        {
            case 11: case 12: case 13:
                return num + "th";
        }
        switch (num % 10)
        {
            case 1: return num + "st";
            case 2: return num + "nd";
            case 3: return num + "rd";
            default: return num + "th";
        }
    }

    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use [!{0} start] to press the display. Use [!{0} press TL/TR/BL/BR/] to press that quadrant. Use [!{0} flip 1 2 1] to flip those switches. Use [!{0} set L M R] to set the sliders to left, middle and right positions and submit. Use [!{0} turn N/E/W/S] to set the dial to that position and submit. On TP, the timer is set to 20 seconds.";
    #pragma warning restore 414

    IEnumerator Press(KMSelectable btn, float delay)
    {
        btn.OnInteract();
        yield return new WaitForSeconds(delay);
    }
    IEnumerator ProcessTwitchCommand (string command)
    {
        string[] dialCmds = new[] { "N", "E", "S", "W", "NORTH", "EAST", "SOUTH", "WEST", "U", "R", "D", "L", "UP", "RIGHT", "DOWN", "LEFT" };
        command = command.Trim().ToUpperInvariant();
        List<string> parameters = command.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (command == "START")
        {
            yield return null;
            yield return Press(displayBtn, 0.1f);
        }
        else if (Regex.IsMatch(command, @"^PRESS\s+[TB][LR]$"))
        {
            string[] abbr = new[] { "TL", "TR", "BL", "BR" };
            yield return null;
            yield return Press(quadrants[Array.IndexOf(abbr, command.TakeLast(2).Join(""))], 0.1f);
        }
        else if (Regex.IsMatch(command, @"^FLIP\s+([12]\s*)+$"))
        {
            yield return null;
            foreach (string str in parameters.Skip(1))
                foreach (int num in str.Select(x => x - '1'))
                    yield return Press(switches[num], 0.55f);
        }
        else if (Regex.IsMatch(command, @"^SET\s+([LMR]\s+){2}[LMR]$"))
        {
            yield return null;
            int[] sets = parameters.Skip(1).Select(x => "LMR".IndexOf(x[0]) - 1).ToArray();
            Debug.Log(sets.Join());
            for (int i = 0; i < 3; i++)
            {
                while (currentSliders[i] < sets[i])
                    yield return Press(slidersRight[i], 0.2f);
                while (currentSliders[i] > sets[i])
                    yield return Press(slidersLeft[i], 0.2f);
            }
            yield return Press(sliderSubmit, 0.1f);
        }
        else if (parameters.Count == 2 && parameters[0] == "TURN" && dialCmds.Contains(parameters[1]))
        {
            yield return null;
            int targetPos = Array.IndexOf(dialCmds, parameters[1]) % 4;
            while (dialPos != targetPos)
                yield return Press(dial, 0.2f);
            yield return Press(dialSubmit, 0.1f);
        }
        else yield break;
        yield return moduleSolved ? "solve" : "strike";
    }

    IEnumerator TwitchHandleForcedSolve ()
    {
        while (coolingDown)
            yield return true;
        if (!started)
            yield return Press(displayBtn, 0.1f);
        yield return new WaitUntil(() => displayText.text != string.Empty);
        if (!stagesSolved[0])
            yield return SolveQuadrants();
        if (!stagesSolved[1])
            yield return SolveSwitches();
        if (!stagesSolved[2])
            yield return SolveSliders();
        if (!stagesSolved[3])
            yield return SolveDial();
        while (timeLerp > 0)
            yield return true;
    }
    IEnumerator SolveQuadrants()
    {
        yield return new WaitUntil(() => correctQuadrant != -1);
        yield return Press(quadrants[correctQuadrant], 0.2f);
    }
    IEnumerator SolveSwitches()
    {
        if (currentSwitches[0] != correctSwitches[0])
            yield return Press(switches[0], 0.55f);
        if (currentSwitches[1] != correctSwitches[1])
            yield return Press(switches[1], 0.55f);
        if (prevSwitch == -1)
            prevSwitch = 1;
        yield return Press(switches[1 - prevSwitch], 0.55f);
        yield return Press(switches[prevSwitch], 0.1f);
    }
    IEnumerator SolveSliders()
    {
        for (int i = 0; i < 3; i++)
        {
            while (currentSliders[i] < correctSliders[i])
                yield return Press(slidersRight[i], 0.3f);
            while (currentSliders[i] > correctSliders[i])
                yield return Press(slidersLeft[i], 0.3f);
        }
        yield return Press(sliderSubmit, 0.3f);
    }
    IEnumerator SolveDial()
    {
        while (!correctDialPositions.Contains(dialPos))
            yield return Press(dial, 0.3f);
        yield return Press(dialSubmit, 0.1f);
    }
}
