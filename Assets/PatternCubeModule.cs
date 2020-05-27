using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PatternCube;
using UnityEngine;
using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Pattern Cube
/// Created by Timwi
/// </summary>
public class PatternCubeModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMSelectable MainSelectable;
    public KMRuleSeedable RuleSeedable;

    public Mesh[] NetMeshes;
    public KMSelectable[] SelectableBoxes;
    public KMSelectable[] PlaceableBoxes;
    public MeshFilter ModuleFront;
    public Texture[] SymbolTextures;
    public Material FrameBlue, FrameGreen, FrameRed, FrameYellow, ScreenDark, ScreenLight;
    public TextMesh[] TpLetters;

    private readonly MeshRenderer[] _selectableSymbolObjs = new MeshRenderer[5];
    private readonly MeshRenderer[] _placeableSymbolObjs = new MeshRenderer[6];
    private readonly MeshRenderer[] _selectableFrameObjs = new MeshRenderer[5];
    private readonly MeshRenderer[] _placeableFrameObjs = new MeshRenderer[6];
    private readonly MeshRenderer[] _selectableScreenObjs = new MeshRenderer[5];
    private readonly MeshRenderer[] _placeableScreenObjs = new MeshRenderer[6];

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private Net _puzzle;
    private FaceSymbol[] _solution;
    private FaceSymbol[] _selectableSymbols;
    private int _faceGivenByHighlight;
    private int _selected = 2;
    private int _highlightedPosition;   // for Souvenir

    private readonly List<int> _facesRevealed = new List<int>();
    private readonly char[] _tpLetters = new char[6];

    private sealed class RotateTask { public int From; public int To; }
    private readonly Queue<RotateTask>[] _queues = new Queue<RotateTask>[5];

    void Start()
    {
        _moduleId = _moduleIdCounter++;
        var numbers = Enumerable.Range(0, 26).Where(i => i != 20).ToList(); // exclude “U” because that’s a valid command (u-turn = 180)
        for (int i = 0; i < 6; i++)
        {
            var ix = Rnd.Range(0, numbers.Count);
            _tpLetters[i] = (char) ('A' + numbers[ix]);
            TpLetters[i].text = _tpLetters[i].ToString();
            TpLetters[i].gameObject.SetActive(false);
            numbers.RemoveAt(ix);
        }

        for (int i = 0; i < 5; i++)
        {
            _selectableSymbolObjs[i] = SelectableBoxes[i].transform.Find("Symbol").GetComponent<MeshRenderer>();
            _selectableFrameObjs[i] = SelectableBoxes[i].transform.Find("Frame").GetComponent<MeshRenderer>();
            _selectableScreenObjs[i] = SelectableBoxes[i].transform.Find("Screen").GetComponent<MeshRenderer>();
            _queues[i] = new Queue<RotateTask>();
            StartCoroutine(rotator(i));
        }
        for (int i = 0; i < 6; i++)
        {
            _placeableSymbolObjs[i] = PlaceableBoxes[i].transform.Find("Symbol").GetComponent<MeshRenderer>();
            _placeableFrameObjs[i] = PlaceableBoxes[i].transform.Find("Frame").GetComponent<MeshRenderer>();
            _placeableScreenObjs[i] = PlaceableBoxes[i].transform.Find("Screen").GetComponent<MeshRenderer>();
        }

        for (int i = 0; i < 25; i++)
            if (i % 5 != 0)
                MainSelectable.Children[i] = null;

        // Generate the rules
        List<int> symbolIxs;
        var rnd = RuleSeedable.GetRNG();
        Debug.LogFormat("[Pattern Cube #{0}] Using rule seed: {1}", _moduleId, rnd.Seed);

        string[] groupStrs = new string[2];
        if (rnd.Seed == 1)
        {
            groupStrs[0] =
                "X1,A3,B1;Y0,X1,A1;B0,D1,C2;Z1,A2,X0;Y2,B0,A2;Y3,C1,B0;" +
                "X2,C0,A1;X1,Y2,B0;C2,A3,D1;Z1,X3,B1;A2,C3,Y2;D1,B2,Y0;" +
                "X2,C2,B1;X1,A2,D2;X2,C3,Y0;D3,A0,B3;X0,Z2,C1;A0,Y3,D0;" +
                "D0,B0,X0;X1,C1,D0;Y0,D0,X3;C3,B3,A1;Z1,D0,X0;C1,D0,Y2";
            groupStrs[1] =
                "X1,E0,F0;X0,E3,Y1;H1,G1,F1;Y3,Z3,E1;Z3,F1,E2;G1,Z1,F1;" +
                "E0,X2,G1;X0,F0,Y2;G1,E2,H3;F1,Y2,Z0;G2,E2,Z1;F1,Z1,H2;" +
                "G1,X1,F0;H3,X2,E2;Y1,X3,G3;H1,E3,F0;G0,Z2,Y0;Z0,H2,E3;" +
                "X3,H1,F0;G1,H3,X0;X3,H3,Y0;E0,F0,G1;Z3,Y3,H1;Z2,H3,G2";
            symbolIxs = new List<int> { 90, 91, 92, 93, 100, 101, 102, 103, 110, 111, 112 };
        }
        else
        {
            var skip = rnd.Next(0, 100);
            for (var i = 0; i < skip; i++)
                rnd.NextDouble();

            symbolIxs = Enumerable.Range(0, 120).ToList();
            rnd.ShuffleFisherYates(symbolIxs);
            var groupsTemplates = new[] {
                "ABX,AXY,BCD,AXZ,ABY,BCY,ACX,BXY,ACD,BXZ,ACY,BDY,BCX,ADX,CXY,ABD,CXZ,ADY,BDX,CDX,DXY,ABC,DXZ,CDY",
                "EFX,EXY,FGH,EYZ,EFZ,FGZ,EGX,FXY,EGH,FYZ,EGZ,FHZ,FGX,EHX,GXY,EFH,GYZ,EHZ,FHX,GHX,HXY,EFG,HYZ,GHZ"
            };
            for (var i = 0; i < 2; i++)
            {
                var grs = groupsTemplates[i].Split(',');
                var arr = new List<string>();
                for (var j = 0; j < grs.Length; j++)
                {
                    var smbs = grs[j].Select(ch => ch.ToString()).ToList();
                    rnd.ShuffleFisherYates(smbs);
                    for (var k = 0; k < smbs.Count; k++)
                        smbs[k] = smbs[k] + rnd.Next(0, 4);
                    arr.Add(smbs.JoinString(","));
                }
                groupStrs[i] = arr.JoinString(";");
            }
        }

        HalfCube[][] groups = groupStrs.Select(str => str.Split(';').Select(hci => hci.Split(',').Select(fc => new FaceSymbol(fc[0], fc[1] - '0')).ToArray()).Select(arr => new HalfCube(arr[0], arr[1], arr[2])).ToArray()).ToArray();

        // Generate a puzzle
        _puzzle = Data.Nets[Rnd.Range(0, Data.Nets.Length)];
        HalfCube[] frontHalfCubes, backHalfCubes;
        if (Rnd.Range(0, 2) == 0)
        {
            frontHalfCubes = groups[0];
            backHalfCubes = groups[1];
        }
        else
        {
            frontHalfCubes = groups[1];
            backHalfCubes = groups[0];
        }
        var frontHalfCube = frontHalfCubes[Rnd.Range(0, frontHalfCubes.Length)];
        var symbolsAlready = new[] { frontHalfCube.Top.Symbol, frontHalfCube.Left.Symbol, frontHalfCube.Front.Symbol };
        var backHalfCubeCandidates = backHalfCubes.Where(ag => !new[] { ag.Top.Symbol, ag.Left.Symbol, ag.Front.Symbol }.Intersect(symbolsAlready).Any()).ToArray();
        var backHalfCube = backHalfCubeCandidates[Rnd.Range(0, backHalfCubeCandidates.Length)];
        FaceSymbol right, back, bottom;
        switch (Rnd.Range(0, 3))
        {
            case 0:
                back = new FaceSymbol(backHalfCube.Front.Symbol, (backHalfCube.Front.Orientation + 3) % 4);
                right = new FaceSymbol(backHalfCube.Top.Symbol, (backHalfCube.Top.Orientation + 3) % 4);
                bottom = new FaceSymbol(backHalfCube.Left.Symbol, (backHalfCube.Left.Orientation + 3) % 4);
                break;
            case 1:
                back = new FaceSymbol(backHalfCube.Top.Symbol, backHalfCube.Top.Orientation);
                right = new FaceSymbol(backHalfCube.Left.Symbol, (backHalfCube.Left.Orientation + 1) % 4);
                bottom = new FaceSymbol(backHalfCube.Front.Symbol, backHalfCube.Front.Orientation);
                break;
            default:
                back = new FaceSymbol(backHalfCube.Left.Symbol, (backHalfCube.Left.Orientation + 2) % 4);
                right = new FaceSymbol(backHalfCube.Front.Symbol, (backHalfCube.Front.Orientation + 2) % 4);
                bottom = new FaceSymbol(backHalfCube.Top.Symbol, (backHalfCube.Top.Orientation + 1) % 4);
                break;
        }
        _faceGivenByHighlight = Rnd.Range(0, 6);
        var faceGivenFully = (new[] { 0, 1, 4 }.Contains(_faceGivenByHighlight) ? new[] { 2, 3, 5 } : new[] { 0, 1, 4 })[Rnd.Range(0, 3)];
        _facesRevealed.Add(faceGivenFully);
        _solution = new[] { frontHalfCube.Top, frontHalfCube.Front, right, back, frontHalfCube.Left, bottom };
        _selectableSymbols = _solution.Except(new[] { _solution[faceGivenFully] }).ToArray().Shuffle();
        for (int i = 0; i < _selectableSymbols.Length; i++)
        {
            _selectableSymbols[i] = new FaceSymbol(_selectableSymbols[i].Symbol, Rnd.Range(0, 4));
            if (_selectableSymbols[i].Symbol == _solution[_faceGivenByHighlight].Symbol)
                _highlightedPosition = i;
        }

        // Populate the selectable boxes
        for (int i = 0; i < 5; i++)
        {
            _selectableSymbolObjs[i].material.mainTexture = SymbolTextures[symbolIxs["ABCDEFGHXYZ".IndexOf(_selectableSymbols[i].Symbol)]];
            _selectableSymbolObjs[i].transform.localEulerAngles = new Vector3(90, _selectableSymbols[i].Orientation * 90, 0);
            _selectableFrameObjs[i].material = FrameRed;
            _selectableScreenObjs[i].material = _selectableSymbols[i].Symbol == _solution[_faceGivenByHighlight].Symbol ? ScreenLight : ScreenDark;
            SelectableBoxes[i].OnInteract = GetSelectableHandler(i);
        }

        // Arrange the placeable boxes
        const float x1 = -.8f;
        const float x2 = .8f;
        const float y1 = -.8f;
        const float y2 = .8f;
        const float w = (x2 - x1) / 5.5f;
        const float h = (y2 - y1) / 5.5f;

        for (int y = 0; y < _puzzle.Faces.GetLength(1); y++)
            for (int x = 0; x < _puzzle.Faces.GetLength(0); x++)
            {
                if (_puzzle.Faces[x, y] == null)
                    continue;
                var fi = _puzzle.Faces[x, y];
                _placeableSymbolObjs[fi.Face].transform.localEulerAngles = new Vector3(90, (fi.Orientation + _solution[fi.Face].Orientation) * 90, 0);
                _placeableSymbolObjs[fi.Face].material.mainTexture = SymbolTextures[symbolIxs["ABCDEFGHXYZ".IndexOf(_solution[fi.Face].Symbol)]];
                _placeableSymbolObjs[fi.Face].gameObject.SetActive(fi.Face == faceGivenFully);
                _placeableFrameObjs[fi.Face].material = fi.Face == faceGivenFully ? FrameGreen : FrameYellow;
                _placeableScreenObjs[fi.Face].material = fi.Face == _faceGivenByHighlight ? ScreenLight : ScreenDark;
                PlaceableBoxes[fi.Face].transform.localPosition = new Vector3(x1 + w * (x + 2), 0, y1 + h * (_puzzle.Faces.GetLength(1) - y - .5f)) * .1f;
                PlaceableBoxes[fi.Face].OnInteract = GetPlaceableHandler(fi);
                MainSelectable.Children[(5 - _puzzle.Faces.GetLength(1) + y) * 5 + x + 1] = PlaceableBoxes[fi.Face];
            }

        MainSelectable.UpdateChildren(SelectableBoxes[0]);

        // Set the correct mesh for the module front
        var id = "ModuleFront_" + _puzzle.ID;
        ModuleFront.mesh = NetMeshes.First(m => m.name == id);

        Log(@"=svg[Puzzle:]<svg xmlns='http://www.w3.org/2000/svg' viewBox='-3 -3 1206 {0}'>{1}</svg>",
            /* {0} */ 120 * _puzzle.Faces.GetLength(1) + 6,
            /* {1} */ Enumerable.Range(0, _puzzle.Faces.GetLength(1)).SelectMany(y => Enumerable.Range(0, _puzzle.Faces.GetLength(0)).Select(x =>
                _puzzle.Faces[x, y] == null ? null :
                _puzzle.Faces[x, y].Face == faceGivenFully ? svg(x, y, symbolIxs["ABCDEFGHXYZ".IndexOf(_solution[_puzzle.Faces[x, y].Face].Symbol)], (_solution[_puzzle.Faces[x, y].Face].Orientation + _puzzle.Faces[x, y].Orientation) % 4) :
                svg(x, y, highlighted: _puzzle.Faces[x, y].Face == _faceGivenByHighlight))).JoinString());

        Log(@"=svg[Symbols:]<svg xmlns='http://www.w3.org/2000/svg' viewBox='-3 -3 1206 126'>{0}</svg>",
            _selectableSymbols.Select((s, ix) => svgPath(ix, 0, symbolIxs["ABCDEFGHXYZ".IndexOf(s.Symbol)], 0)).JoinString());
        Log(@" The highlighted symbol is {0}.", "first,second,third,4th,5th".Split(',')[_highlightedPosition]);

        Log(@"=svg[Solution:]<svg xmlns='http://www.w3.org/2000/svg' viewBox='-3 -3 1206 {0}'>{1}</svg>",
            /* {0} */ 120 * _puzzle.Faces.GetLength(1) + 6,
            /* {1} */ Enumerable.Range(0, _puzzle.Faces.GetLength(1)).SelectMany(y => Enumerable.Range(0, _puzzle.Faces.GetLength(0)).Select(x =>
                _puzzle.Faces[x, y] == null ? null :
                svg(x, y, symbolIxs["ABCDEFGHXYZ".IndexOf(_solution[_puzzle.Faces[x, y].Face].Symbol)], (_solution[_puzzle.Faces[x, y].Face].Orientation + _puzzle.Faces[x, y].Orientation) % 4))).JoinString());

        AssignSymbols();
        // Because TP doesn’t set TwitchPlaysActive soon enough, we have to delay the TP letter initialization
        Module.OnActivate += delegate { AssignSymbols(); };
    }

    private static string svg(int x, int y, int? symbol = null, int orientation = -1, bool highlighted = false)
    {
        var rect = string.Format("<rect x='{0}' y='{1}' width='120' height='120' fill='{2}' stroke='black' stroke-width='2'/>", x * 120, y * 120, highlighted ? "#f88" : "none");
        return symbol == null ? rect : rect + svgPath(x, y, symbol.Value, orientation);
    }
    private static string svgPath(int x, int y, int symbol, int orientation)
    {
        return string.Format("<path d='{5}' transform='translate({3}, {4}) rotate({2}) translate({0}, {1})'/>", -120 * (symbol % 10) - 60, -120 * (symbol / 10) - 60, 90 * orientation, 120 * x + 60, 120 * y + 60, Data.Svgs[symbol]);
    }

    private IEnumerator rotator(int ix)
    {
        const float duration = .15f;

        while (true)
        {
            yield return null;
            if (_queues[ix].Count == 0)
                continue;

            var elem = _queues[ix].Dequeue();
            var elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.deltaTime;
                var t = Easing.OutSine(Mathf.Min(elapsed, duration), 0, 1, duration);
                _selectableSymbolObjs[ix].transform.localRotation = Quaternion.Slerp(Quaternion.Euler(90, elem.From * 90, 0), Quaternion.Euler(90, elem.To * 90, 0), t);
            }
        }
    }

    private string str(FaceInfo fi)
    {
        if (fi == null)
            return "-";
        var fs = _solution[fi.Face];
        return fs.Symbol + "NESW".Substring((fs.Orientation + fi.Orientation) % 4, 1);
    }

    private KMSelectable.OnInteractHandler GetPlaceableHandler(FaceInfo fi)
    {
        return delegate
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, PlaceableBoxes[fi.Face].transform);
            PlaceableBoxes[fi.Face].AddInteractionPunch();

            if (!_selectableSymbols.Any(sel => sel != null && sel.Symbol == _solution[fi.Face].Symbol))
                // The user clicked on a square that is already filled; allow that and do nothing.
                return false;

            if (_selected == -1)
            {
                Log(" You tried to place a symbol without selecting a symbol first. Strike.");
                Module.HandleStrike();
            }
            else if (_selectableSymbols[_selected] == null)
            {
                Log(" You tried to place a symbol that you already placed. Strike.");
                Module.HandleStrike();
            }
            else
            {
                var sym = _selectableSymbols[_selected];
                if (sym.Symbol != _solution[fi.Face].Symbol)
                {
                    Log(" You tried to place symbol {0} where symbol {1} should have gone. Strike.", _selected + 1, _selectableSymbols.IndexOf(fs => fs != null && fs.Symbol == _solution[fi.Face].Symbol) + 1);
                    Module.HandleStrike();
                }
                else if (sym.Orientation != (_solution[fi.Face].Orientation + fi.Orientation) % 4)
                {
                    Log(" You tried to place symbol {0} in the correct place, but wrong orientation ({1} instead of {2}). Strike.", _selected + 1, "NESW"[sym.Orientation], "NESW"[(_solution[fi.Face].Orientation + fi.Orientation) % 4]);
                    Module.HandleStrike();
                }
                else
                {
                    // Correct placement
                    Log(" Symbol {0} placed correctly.", _selected + 1);
                    _selectableSymbols[_selected] = null;
                    AssignSymbols();
                    StartCoroutine(animatePlacedSymbol(sym.Symbol));
                    if (_selectableSymbols.All(s => s == null))
                    {
                        Log(" Module solved.");
                        Module.HandlePass();
                    }
                }
            }
            return false;
        };
    }

    private IEnumerator animatePlacedSymbol(char symbol)
    {
        var ix = -1;
        for (int i = 0; i < 6; i++)
            if (_solution[i].Symbol == symbol)
            {
                ix = i;
                break;
            }
        if (ix == -1)
            yield break;

        const float duration = .25f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Easing.OutSine(Mathf.Min(elapsed, duration), 0, 1, duration);
            _placeableSymbolObjs[ix].transform.localScale = Vector3.Lerp(new Vector3(.04f, .04f, .04f), new Vector3(.025f, .025f, .025f), t);
            _placeableSymbolObjs[ix].transform.localPosition = Vector3.Lerp(new Vector3(0, .016f, 0), new Vector3(0, .01401f, 0), t);
            yield return null;
        }
    }

    private KMSelectable.OnInteractHandler GetSelectableHandler(int ix)
    {
        return delegate
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, SelectableBoxes[ix].transform);
            SelectableBoxes[ix].AddInteractionPunch();

            if (_selectableSymbols[ix] == null)
                // The user clicked on a symbol that is already on the net; allow that and do nothing.
                return false;

            if (ix != _selected)
                // Just update the selection
                _selected = ix;
            else
            {
                var tsk = new RotateTask { From = _selectableSymbols[ix].Orientation, To = (_selectableSymbols[ix].Orientation + 1) % 4 };
                _selectableSymbols[ix] = new FaceSymbol(_selectableSymbols[ix].Symbol, tsk.To);
                _queues[ix].Enqueue(tsk);
            }
            AssignSymbols();
            return false;
        };
    }

    private void AssignSymbols()
    {
        // Selectable boxes
        for (int i = 0; i < 5; i++)
        {
            if (_selectableSymbols[i] == null)
                _selectableSymbolObjs[i].gameObject.SetActive(false);
            _selectableFrameObjs[i].material = _selectableSymbols[i] == null ? FrameBlue : i == _selected ? FrameGreen : FrameRed;
            _selectableScreenObjs[i].material = _selectableSymbols[i] != null && _selectableSymbols[i].Symbol == _solution[_faceGivenByHighlight].Symbol ? ScreenLight : ScreenDark;
        }

        // Placeable boxes
        for (int y = 0; y < _puzzle.Faces.GetLength(1); y++)
            for (int x = 0; x < _puzzle.Faces.GetLength(0); x++)
            {
                if (_puzzle.Faces[x, y] == null)
                    continue;
                var face = _puzzle.Faces[x, y].Face;
                var isPlaced = !_selectableSymbols.Any(sel => sel != null && sel.Symbol == _solution[face].Symbol);
                _placeableSymbolObjs[face].gameObject.SetActive(isPlaced);
                _placeableFrameObjs[face].material = isPlaced ? FrameGreen : FrameYellow;
                _placeableScreenObjs[face].material = face == _faceGivenByHighlight && !isPlaced ? ScreenLight : ScreenDark;
                TpLetters[face].gameObject.SetActive(TwitchPlaysActive && !isPlaced);
            }
    }

    void Log(string msg, params object[] fmtArgs)
    {
        Debug.LogFormat(@"[Pattern Cube #{0}]{1}", _moduleId, string.Format(msg, fmtArgs));
    }

#pragma warning disable 414
#pragma warning disable IDE0044 // Add readonly modifier
    private readonly string TwitchHelpMessage = @"!{0} rotate 1 cw/ccw/180 [rotate the first symbol] | !{0} place 1 in C [place the first symbol in the box with letter C] | Abbreviate: !{0} 1 cw 1 c";
    private bool TwitchPlaysActive = false;
#pragma warning restore IDE0044 // Add readonly modifier
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        var pieces = Regex.Matches(command, @"(\brotate\b|\bturn\b|\bplace\b|\s|,|(?<rotate>\d +(?:cw|ccw|180|u))|(?<place>(?<num>\d) +(?:in +)?(?<loc>[a-z])))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var commands = new List<object>();
        var strIx = 0;
        foreach (Match piece in pieces)
        {
            if (piece.Index != strIx)
                yield break;
            strIx += piece.Length;
            if (piece.Groups["place"].Success)
            {
                var ix = int.Parse(piece.Groups["num"].Value) - 1;
                var cell = Array.IndexOf(_tpLetters, piece.Groups["loc"].Value.ToUpperInvariant()[0]);
                if (cell == -1 || ix < 0 || ix >= 5)
                    yield break;
                commands.Add(new Action(() => { if (_selected != ix) SelectableBoxes[ix].OnInteract(); }));
                commands.Add(new Action(() => { if (_selected == ix) PlaceableBoxes[cell].OnInteract(); }));
            }
            else if (piece.Groups["rotate"].Success)
            {
                var spl = piece.Groups["rotate"].Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var ix = int.Parse(spl[0]) - 1;
                if (ix < 0 || ix >= 5)
                    yield break;
                var rotation =
                    spl[1].Equals("cw", StringComparison.InvariantCultureIgnoreCase) ? 1 :
                    spl[1].Equals("ccw", StringComparison.InvariantCultureIgnoreCase) ? 3 : 2;
                commands.Add(new Action(() => { if (_selected != ix) SelectableBoxes[ix].OnInteract(); }));
                for (int i = 0; i < rotation; i++)
                    commands.Add(new Action(() => { SelectableBoxes[ix].OnInteract(); }));
            }
        }

        foreach (var action in commands)
        {
            yield return null;
            if (action is Action)
                ((Action) action)();
            yield return new WaitForSeconds(.1f);
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        for (var x = 0; x < _puzzle.Faces.GetLength(0); x++)
            for (var y = 0; y < _puzzle.Faces.GetLength(1); y++)
            {
                if (_puzzle.Faces[x, y] == null)
                    continue;
                var solution = _solution[_puzzle.Faces[x, y].Face];
                var leftIx = _selectableSymbols.IndexOf(fs => fs != null && fs.Symbol == solution.Symbol);
                if (leftIx == -1)
                    continue;
                do
                {
                    SelectableBoxes[leftIx].OnInteract();
                    yield return new WaitForSeconds(.1f);
                }
                while (_selectableSymbols[leftIx].Orientation != (solution.Orientation + _puzzle.Faces[x, y].Orientation) % 4);

                yield return new WaitForSeconds(.2f);
                PlaceableBoxes[_puzzle.Faces[x, y].Face].OnInteract();
                yield return new WaitForSeconds(.3f);
            }
    }
}