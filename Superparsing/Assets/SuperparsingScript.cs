using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;

public class SuperparsingScript : MonoBehaviour {

    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMBombModule Module;

    public KMSelectable wordDisplay;
    public KMSelectable[] quadrants;
    public KMSelectable[] switches;
    public KMSelectable[] slidersLeft;
    public KMSelectable[] slidersRight;
    public KMSelectable sliderSubmit;
    public KMSelectable dial;
    public KMSelectable dialSubmit;
    public GameObject modBG;
    public GameObject meter;
    public MeshRenderer[] highlights;
    public Material highlightcol;
    private float timeLimit = 10f;
    private float timeLerp = 0f;
    private Coroutine timerCor;
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
    private readonly string[] switchPositions = new[] { "Down", "Up" };
    private readonly string[] sliderPositions = new[] { "Left", "Middle", "Right" };
    private readonly string[] dialPositions = new[] { "North", "East", "South", "West   " };
    int correctQuadrant;
    bool[] currentSwitches = new bool[2] { true, true };
    bool[] correctSwitches = new bool[2];
    bool[] switchesMoving = new bool[2];
    int prevSwitch = -1;
    int[] currentSliders = new int[3];
    int[] correctSliders = new int[3];
    Coroutine[] sliderMovements = new Coroutine[3];
    bool dialSpinning;
    Queue<IEnumerator> dialMovements = new Queue<IEnumerator>();
    int dialPos;
    int[] correctDialPositions;
    bool dialFlipped;
    public bool TwitchPlaysActive;

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
        wordDisplay.OnInteract += delegate () { StartTimer(); return false; };
        for (int i = 0; i < 4; i++)
            squareTransforms[i] = squares[i].transform.localPosition;
        Module.OnActivate += delegate () { Audio.PlaySoundAtTransform("startup", transform); if (TwitchPlaysActive) timeLimit = 20; };
    }

    void Start ()
    {
        wordDisplay.GetComponentInChildren<TextMesh>().text = string.Empty;
        RandomizeData();
    }
    void RandomizeData()
    {
        squarePositions.Shuffle();
        for (int i = 0; i < 4; i++)
            squares[squarePositions[i]].transform.localPosition = squareTransforms[i];
        Debug.LogFormat("[Superparsing #{0}] The squares in reading order are: {1}.", moduleId, squarePositions.Select(x => romanNumerals[x]).Join(", "));
        for (int i = 0; i < 2; i++)
            if (UnityEngine.Random.Range(0, 2) == 0)
            {
                switchHandles[i].transform.localEulerAngles = new Vector3(-70, 0, 0); //Flips the switch position;
                currentSwitches[i] = false;
            }
        dialPos = UnityEngine.Random.Range(0, 4);
        dial.transform.localEulerAngles = new Vector3(0, 0, 90) * dialPos;
        for (int i = 0; i < 3; i++)
        {
            int move = UnityEngine.Random.Range(-1, 2);//-1, 0, or 1;
            currentSliders[i] = move;
            sliderKnobs[i].transform.localPosition += new Vector3(0.25f, 0, 0) * move;
        }
        if (UnityEngine.Random.Range(0,2) == 0)
        {
            dialFlipped = true;
            dialParent.transform.localPosition += 0.3f * Vector3.right; //The dial is stored under a separate gameobject, so we need to drill into the parent
            dialSubmit.transform.localPosition += 0.6f * Vector3.left;
        }
    }

    #region Timer Methods
    void Update ()
    {
        meter.SetActive(timeLerp > 0);
        meter.transform.localScale = new Vector3(1, 1, timeLerp);
        modBG.GetComponent<MeshRenderer>().material.color = Color.Lerp("2D1C1C".Color(), "FF2020".Color(), Mathf.Pow(timeLerp, 3));
        modBG.transform.localPosition = new Vector3(UnityEngine.Random.Range(-0.025f, 0.025f), 0, UnityEngine.Random.Range(-0.025f, 0.025f)) * Mathf.Pow(timeLerp, 6);
    }
    IEnumerator HeatUp()
    {
        while (true)
        {
            while (timeLerp < 1)
            {
                if (started && !coolingDown)
                    timeLerp += 1f / timeLimit * Time.deltaTime;
                yield return null;
            }
            if (timeLerp >= 1)
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
    {
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
        if (stagesSolved[0] || !started)
            return;
        quadrants[pos].AddInteractionPunch(0.3f);
        if (pos == correctQuadrant)
        {
            Audio.PlaySoundAtTransform("quadrantpress", quadrants[pos].transform);
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
    {
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
        currentSwitches[pos] = !currentSwitches[pos];
        Audio.PlaySoundAtTransform("lever", switches[pos].transform);
        StartCoroutine(MoveSwitch(pos));
        if (!started || stagesSolved[1])
            return;
        if (prevSwitch == pos)
            StartCoroutine(SwitchSubmit());
        prevSwitch = pos;
    }
    IEnumerator SwitchSubmit()
    {
        yield return new WaitForSeconds(0.3f);
        if (currentSwitches.SequenceEqual(correctSwitches))
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
    IEnumerator MoveSwitch(int pos)
    {
        switchesMoving[pos] = true;
        Transform TF = switchHandles[pos].transform;
        float from = currentSwitches[pos] ? -70 : 70;
        float to = -1 * from;
        float startTime = Time.fixedTime;
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
        if (currentSliders[pos] + direction < -1 || currentSliders[pos] + direction > 1)
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
        sliderSubmit.AddInteractionPunch(1);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, sliderSubmit.transform);
        if (stagesSolved[2] || !started)
            return;
        if (currentSliders.SequenceEqual(correctSliders))
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
        dialMovements.Enqueue(DialSpin());
        if (!dialSpinning)
            StartCoroutine(dialMovements.Dequeue());
    }
    IEnumerator DialSpin()
    {
        dialSpinning = true;
        for (int i = 0; i < 15; i++)
        {
            dial.transform.localEulerAngles += 6 * Vector3.forward;
            yield return null;
        }
        dialSpinning = false;
        if (dialMovements.Count != 0)
            StartCoroutine(dialMovements.Dequeue());

    }
    void SubmitDial()
    {
        dial.AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, dial.transform);
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
        wordDisplay.AddInteractionPunch(2.5f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, wordDisplay.transform);
        if (started || moduleSolved)
            return;
        StartCoroutine(Begin());

    }
    #endregion

    void Solve(int pos)
    {
        coolingDown = true;
        stagesSolved[pos] = true;
        StartCoroutine(ShowHighlight(pos, false));
        if (stagesSolved.All(x => x))
            StartCoroutine(SolveModule());
        else StartCoroutine(ReduceTimer(timeLerp / (0.4f * timeLimit)));
        Audio.PlaySoundAtTransform("shimmer", transform);
    }
    IEnumerator ShowHighlight(int pos, bool reverse)
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
        wordDisplay.GetComponentInChildren<TextMesh>().text = string.Empty;
        for (int i = 0; i < 4; i++)
            StartCoroutine(ShowHighlight(i, true));
        StartCoroutine(ReduceTimer(0, 0.15f));
        yield return new WaitUntil(() => timeLerp <= 0);
        Audio.PlaySoundAtTransform("solve", transform);
        Module.HandlePass();
    }

    void Strike()
    {
        started = false;
        for (int i = 0; i < 4; i++)
        {
            if (stagesSolved[i])
                StartCoroutine(ShowHighlight(i, true));
            stagesSolved[i] = false;
            StartCoroutine(QuadrantsFade("784B4B".Color()));
        }
        wordDisplay.GetComponentInChildren<TextMesh>().text = string.Empty;
        prevSwitch = -1;
        Module.HandleStrike();
        StartCoroutine(ReduceTimer(0));
    }
    IEnumerator Begin()
    {
        yield return null;
        yield return new WaitForSeconds(1);
        started = true;
        displayedWord = WordList.words.Where(word => word.Any(ch => GetDialString().Contains(ch))).PickRandom(); //Guarantees that there's a valid dial answer.
        wordDisplay.GetComponentInChildren<TextMesh>().text = displayedWord;
        Debug.LogFormat("[Superparsing #{0}] The displayed word is {1}. Let the games begin.", moduleId, displayedWord);
        CalculateQuadrants();
        CalculateSwitches();
        CalculateSliders();
        CalculateDial();
        yield return new WaitForSeconds(1);
        if (heatUp == null)
            heatUp = StartCoroutine(HeatUp());
    }
    #region Module Calculations
    void CalculateQuadrants()
    {
        Debug.LogFormat("[Superparsing #{0}] ::QUADRANTS::", moduleId);
        int ix = int.Parse(Bomb.GetSerialNumberNumbers().Join("")) % 4;
        int[] sortedOrder = Enumerable.Range(0, 4).OrderBy(x => displayedWord[x]).ToArray();
        correctQuadrant = sortedOrder[ix];
        Debug.LogFormat("[Superparsing #{0}] The order generated from the word is {1}. Indexing into this by the serial mod 4 yields {2}.", moduleId, sortedOrder.Select(x => x + 1).Join(), correctQuadrant + 1);
        Debug.LogFormat("[Superparsing #{0}] You should press the {1} quadrant.", moduleId, quadrantPositions[correctQuadrant]);
    }
    void CalculateSwitches()
    {
        Debug.LogFormat("[Superparsing #{0}] ::SWITCHES::", moduleId);
        Predicate<char> condition;
        string loggingString = "For the letter to represent a 1,  the letter must ";
        switch (Bomb.GetOnIndicators().Count() - Bomb.GetOffIndicators().Count())
        {
            case -2: loggingString += string.Format("be before {0} in the alphabet.", Bomb.GetSerialNumber()[3]); condition = x => x < Bomb.GetSerialNumber()[3]; break;
            case -1: loggingString += "be within the range A-M inclusive."; condition = x => x - 'A' < 13; break;
            case 0: loggingString += "be a vowel."; condition = x => "AEIOU".Contains(x); break;
            case 1: loggingString += "be within the range N-Z inclusive."; condition = x => x - 'A' >= 13; break;
            case 2: loggingString += string.Format("be later than {0} in the alphabet.", Bomb.GetSerialNumber()[4]); condition = x => x > Bomb.GetSerialNumber()[4]; break;
            default: loggingString += "be a consonant."; condition = x => !"AEIOU".Contains(x); break;
        }
        bool[] binary = displayedWord.Select(x => condition(x)).ToArray();
        Debug.LogFormat("[Superparsing #{0}] {1}", moduleId, loggingString);
        Debug.LogFormat("[Superparsing #{0}] The generated binary from the word is {1}.", moduleId, binary.Select(x => x ? 'T' : 'F').Join(""));
        for (int i = 0; i < 2; i++)
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

    IEnumerator ProcessTwitchCommand (string command)
    {
        string[] dialCmds = new[] { "N", "E", "S", "W", "NORTH", "EAST", "SOUTH", "WEST", "U", "R", "D", "L", "UP", "RIGHT", "DOWN", "LEFT" };
        command = command.Trim().ToUpperInvariant();
        List<string> parameters = command.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (command == "START")
        {
            yield return null;
            wordDisplay.OnInteract();
        }
        else if (Regex.IsMatch(command, @"^PRESS\s+[TB][LR]$"))
        {
            string[] abbr = new[] { "TL", "TR", "BL", "BR" };
            yield return null;
            quadrants[Array.IndexOf(abbr, command.TakeLast(2).Join(""))].OnInteract();
        }
        else if (Regex.IsMatch(command, @"^FLIP\s+([12]\s*)+$"))
        {
            yield return null;
            foreach (string str in parameters.Skip(1))
            {
                foreach (int num in str.Select(x => x - '1'))
                {
                    switches[num].OnInteract();
                    yield return new WaitForSeconds(0.55f);
                }
            }
        }
        else if (Regex.IsMatch(command, @"^SET\s+([LMR]\s+){2}[LMR]$"))
        {
            yield return null;
            int[] sets = parameters.Skip(1).Select(x => "LMR".IndexOf(x[0]) - 1).ToArray();
            Debug.Log(sets.Join());
            for (int i = 0; i < 3; i++)
            {
                while (currentSliders[i] < sets[i])
                {
                    slidersRight[i].OnInteract();
                    yield return new WaitForSeconds(0.2f);
                }
                while (currentSliders[i] > sets[i])
                {
                    slidersLeft[i].OnInteract();
                    yield return new WaitForSeconds(0.2f);
                }
            }
            sliderSubmit.OnInteract();
        }
        else if (parameters.Count == 2 && parameters[0] == "TURN" && dialCmds.Contains(parameters[1]))
        {
            yield return null;
            int targetPos = Array.IndexOf(dialCmds, parameters[1]) % 4;
            while (dialPos != targetPos)
            {
                dial.OnInteract();
                yield return new WaitForSeconds(0.2f);
            }
            dialSubmit.OnInteract();
        }
        else yield break;
        yield return moduleSolved ? "solve" : "strike";
    }

    IEnumerator TwitchHandleForcedSolve ()
    {
        while (coolingDown)
            yield return true;
        if (!started)
            wordDisplay.OnInteract();
        while (!started)
            yield return null;
        if (!stagesSolved[0])
        {
            quadrants[correctQuadrant].OnInteract();
            yield return new WaitForSeconds(0.2f);
        }
        if (!stagesSolved[1])
        {
            if (currentSwitches[0] != correctSwitches[0])
            {
                switches[0].OnInteract();
                yield return new WaitForSeconds(0.55f);
            }
            if (currentSwitches[1] != correctSwitches[1])
            {
                switches[1].OnInteract();
                yield return new WaitForSeconds(0.55f); 
            }
            if (prevSwitch == -1)
                prevSwitch = 1;
            switches[1 - prevSwitch].OnInteract();
            yield return new WaitForSeconds(0.55f);
            switches[prevSwitch].OnInteract();
        }
        if (!stagesSolved[2])
        {
            for (int i = 0; i < 3; i++)
            {
                while (currentSliders[i] < correctSliders[i])
                {
                    slidersRight[i].OnInteract();
                    yield return new WaitForSeconds(0.3f);
                }
                while (currentSliders[i] > correctSliders[i])
                {
                    slidersLeft[i].OnInteract();
                    yield return new WaitForSeconds(0.3f);
                }
            }
            sliderSubmit.OnInteract();
            yield return new WaitForSeconds(0.3f);
        }
        if (!stagesSolved[3])
        {
            while (!correctDialPositions.Contains(dialPos))
            {
                dial.OnInteract();
                yield return new WaitForSeconds(0.3f);
            }
            dialSubmit.OnInteract();
        }
        while (timeLerp > 0)
            yield return true;
    }
}
