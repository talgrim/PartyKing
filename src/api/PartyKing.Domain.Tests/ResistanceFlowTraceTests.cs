using System.Text;
using System.Text.RegularExpressions;
using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Domain.Tests;

/// <summary>
/// The path a whole game takes through the phases, over the rules modules that add steps to the flow (the plot cards,
/// the Inquisitor, the Defectors, the Assassin). A scripted player plays each game to its end through the public
/// actions, and the phase after every step is recorded, so the places where the modules hook into the flow (a Defector
/// card flipped as a mission begins, the Assassination instead of the end, the Inquisition after a mission) are pinned
/// by what they lead to. The snapshot is <c>Golden/ResistanceFlowTrace.txt</c>.
/// </summary>
/// <remarks>
/// Each line is one game: the rules, the script and the player count, then one token per phase change: the mission, the
/// phase (see <see cref="Code"/>) and, with Defectors in the game, in braces the side each one is on (S or R). After
/// the arrow come the winner and the missions in order (R resistance, S spies). A change of behaviour fails with the
/// first differing game; when it is intended, regenerate with
/// <c>PARTYKING_UPDATE_GOLDEN=1 dotnet test --filter ResistanceFlowTraceTests</c> and review the diff. The order of a hook
/// against the plot card windows is not visible here, as both effects land before the next token is recorded: it is the
/// contract of <c>IFlowBehavior</c> (a hook is asked right after the phase is set, before the window opens).
/// </remarks>
public class ResistanceFlowTraceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private const string GoldenFile = "ResistanceFlowTrace.txt";

    private const int StepLimit = 600;

    private static readonly int[] PlayerCounts = [5, 7];

    /// <summary>The snapshot takes a game's worth of plays 168 times, so the tests share one.</summary>
    private static readonly Lazy<string> Pinned = new(Snapshot);

    private sealed record Config(string Name, Action<ResistanceRules> Configure);

    /// <summary>How the game is played: which missions the spies fail (S) or the resistance wins (R), and how many teams are voted down first.</summary>
    private sealed record Script(string Name, string Missions, int RejectedFirst);

    private static readonly Config[] Configs =
    [
        new("-", _ => { }),
        new("Inquisitor", rules => rules.InquisitorEnabled = true),
        new("Assassin", rules => rules.AssassinEnabled = true),
        new("Assassin+Inquisitor", rules =>
        {
            rules.AssassinEnabled = true;
            rules.InquisitorEnabled = true;
        }),
        new("Defector", rules => rules.DefectorEnabled = true),
        new("Defector+Inquisitor", rules =>
        {
            rules.DefectorEnabled = true;
            rules.InquisitorEnabled = true;
        }),
        new("Defector+PlotCards", rules =>
        {
            rules.DefectorEnabled = true;
            rules.PlotCardsEnabled = true;
        }),
        new("Defector+PlotCards+Inquisitor", rules =>
        {
            rules.DefectorEnabled = true;
            rules.PlotCardsEnabled = true;
            rules.InquisitorEnabled = true;
        }),
        new("PlotCards", rules => rules.PlotCardsEnabled = true),
        new("PlotCards+Inquisitor", rules =>
        {
            rules.PlotCardsEnabled = true;
            rules.InquisitorEnabled = true;
        }),
        new("Assassin+PlotCards", rules =>
        {
            rules.AssassinEnabled = true;
            rules.PlotCardsEnabled = true;
        }),
        new("Assassin+PlotCards+Inquisitor", rules =>
        {
            rules.AssassinEnabled = true;
            rules.PlotCardsEnabled = true;
            rules.InquisitorEnabled = true;
        }),
        new("Reverser+PlotCards+Inquisitor", rules =>
        {
            rules.ReverserEnabled = true;
            rules.PlotCardsEnabled = true;
            rules.InquisitorEnabled = true;
        }),
        new("BlindSpies+Defector", rules =>
        {
            rules.BlindSpiesEnabled = true;
            rules.DefectorEnabled = true;
        }),
    ];

    private static readonly Script[] Scripts =
    [
        new("RRR", "RRR", 0),
        new("SSS", "SSS", 0),
        new("RSRSR", "RSRSR", 0),
        new("SRSRS", "SRSRS", 0),
        new("RRR+1rejected", "RRR", 1),
        new("RRR+5rejected", "RRR", 5),
    ];

    /// <summary>A short name for each phase, so that a line of the snapshot stays readable.</summary>
    private static string Code(ResistancePhase phase)
    {
        return phase switch
        {
            ResistancePhase.SelectingTeam => "Sel",
            ResistancePhase.Voting => "Vot",
            ResistancePhase.VoteResult => "VoR",
            ResistancePhase.GameOver => "End",
            ResistancePhase.PlayingMission => "Pla",
            ResistancePhase.MissionResult => "Res",
            ResistancePhase.DealingCards => "Dea",
            ResistancePhase.ResolvingCards => "Rco",
            ResistancePhase.LeaderWindow => "Led",
            ResistancePhase.ConfidenceWindow => "Con",
            ResistancePhase.PeekWindow => "Pee",
            ResistancePhase.Inquisition => "Inq",
            ResistancePhase.Assassination => "Ass",
            _ => throw new InvalidOperationException($"No code for the phase {phase}."),
        };
    }

    internal static string Snapshot()
    {
        var text = new StringBuilder();
        for (var config = 0; config < Configs.Length; config++)
        {
            for (var script = 0; script < Scripts.Length; script++)
            {
                foreach (var count in PlayerCounts)
                {
                    // A seed of its own per game, so that the Defector deck and the plot deck differ between them
                    var seed = 3000 + (config * 100) + (script * 10) + count;
                    text.Append(Play(Configs[config], Scripts[script], count, seed)).Append('\n');
                }
            }
        }

        return text.ToString();
    }

    private static string Play(Config config, Script script, int count, int seed)
    {
        var rules = ResistanceRules.Default();
        config.Configure(rules);

        var game = new ResistanceGame { SessionId = Guid.NewGuid(), IsOpen = true, Rules = rules };
        for (var i = 0; i < count; i++)
        {
            game.Players.Add(new ResistancePlayer { PlayerId = Guid.NewGuid(), Order = i });
        }

        var name = $"{config.Name}/{script.Name}/{count}";
        if (game.Start(Now, new Random(seed)).Failure is { } failure)
        {
            return $"{name} => refused {failure}";
        }

        return $"{name} => {new ScriptedGame(game, script).Play()}";
    }

    /// <summary>A table of players who all do the same thing every time: it plays one game to its end through the public actions.</summary>
    private sealed class ScriptedGame
    {
        private readonly ResistanceGame _game;
        private readonly Script _script;
        private readonly Random _random = new(5);
        private readonly HashSet<int> _played = [];
        private int _rejectionsLeft;
        private bool _rejecting;

        public ScriptedGame(ResistanceGame game, Script script)
        {
            _game = game;
            _script = script;
            _rejectionsLeft = script.RejectedFirst;
        }

        private List<ResistancePlayer> Seats => _game.Players.OrderBy(x => x.Order).ToList();

        public string Play()
        {
            var tokens = new List<string> { Token() };
            for (var step = 0; step < StepLimit && _game.Phase != ResistancePhase.GameOver; step++)
            {
                Act();
                var token = Token();
                if (token != tokens[^1])
                {
                    tokens.Add(token);
                }
            }

            if (_game.Phase != ResistancePhase.GameOver)
            {
                tokens.Add("STUCK");
            }

            var missions = string.Concat(_game.MissionResults.OrderBy(x => x.MissionNumber).Select(x => x.Succeeded ? 'R' : 'S'));

            var winner = _game.Winner?.ToString() ?? "none";

            return $"{string.Join(' ', tokens)} -> {winner}{(missions.Length == 0 ? "" : " " + missions)}";
        }

        private string Token()
        {
            var defectors = _game.Players
                .Where(x => x.Character == ResistanceCharacter.Defector)
                .OrderBy(x => x.Order)
                .Select(x => x.Role == ResistanceRole.Spy ? 'S' : 'R')
                .ToArray();
            var sides = defectors.Length == 0 ? "" : "{" + new string(defectors) + "}";

            return $"{_game.MissionNumber}{Code(_game.Phase)}{sides}";
        }

        private void Act()
        {
            switch (_game.Phase)
            {
                case ResistancePhase.SelectingTeam:
                    SelectTeam();
                    break;
                case ResistancePhase.Voting:
                    Vote();
                    break;
                case ResistancePhase.DealingCards:
                    Deal();
                    break;
                case ResistancePhase.ResolvingCards:
                    ResolveCards();
                    break;
                case ResistancePhase.LeaderWindow:
                case ResistancePhase.ConfidenceWindow:
                case ResistancePhase.PeekWindow:
                    Window();
                    break;
                case ResistancePhase.PlayingMission:
                    PlayMission();
                    break;
                case ResistancePhase.Inquisition:
                    Inquire();
                    break;
                case ResistancePhase.MissionResult:
                    _game.Continue(_game.LeaderPlayerId, false);
                    break;
                case ResistancePhase.Assassination:
                    Assassinate();
                    break;
            }
        }

        /// <summary>Whether the script has the spies fail the current mission (the last character of the script goes on for the missions after it).</summary>
        private bool SpiesFail()
        {
            var index = Math.Min(_game.MissionNumber, _script.Missions.Length) - 1;

            return _script.Missions[index] == 'S';
        }

        private void SelectTeam()
        {
            foreach (var card in HandCards(ResistancePlotCardType.TakeResponsibility))
            {
                TakeResponsibility(card);
            }

            if (_game.Phase != ResistancePhase.SelectingTeam)
            {
                return;
            }

            // The side that is to win the mission picks its own, so the script decides the result whoever leads
            var seats = Seats;
            var wanted = SpiesFail() ? ResistanceRole.Spy : ResistanceRole.Resistance;
            var team = seats
                .OrderBy(x => x.Role == wanted ? 0 : 1)
                .Take(_game.Rules.TeamSize(seats.Count, _game.MissionNumber))
                .Select(x => x.PlayerId)
                .ToList();
            _game.SelectTeam(_game.LeaderPlayerId, team);
        }

        private void TakeResponsibility(ResistancePlotCard card)
        {
            _played.Add(card.CardId);
            var target = Seats.First(x => x.PlayerId != card.HolderPlayerId).PlayerId;
            var outcome = _game.TakeResponsibility(card.HolderPlayerId, card.CardId, target, null, Now, _random);
            if (outcome.Failure == ResistanceFailure.ChoiceRequired)
            {
                _game.TakeResponsibility(card.HolderPlayerId, card.CardId, target, false, Now, _random);
            }
        }

        private void Vote()
        {
            var seats = Seats;
            if (seats.All(x => x.Vote is null))
            {
                _rejecting = _rejectionsLeft > 0;
                if (_rejecting)
                {
                    _rejectionsLeft--;
                }
            }

            // The Opinion Makers have to vote first: whoever is refused is tried again at the next step
            foreach (var seat in seats.Where(x => x.Vote is null))
            {
                if (_game.Phase != ResistancePhase.Voting)
                {
                    return;
                }

                _game.Vote(seat.PlayerId, !_rejecting);
            }
        }

        private void Deal()
        {
            var others = Seats.Where(x => x.PlayerId != _game.LeaderPlayerId).ToList();
            var assignments = ResistancePlotCardQueries.CardsToDeal(_game)
                .OrderBy(x => x.CardId)
                .Select((card, i) => new PlotCardAssignment(card.CardId, others[(i + _game.MissionNumber) % others.Count].PlayerId))
                .ToList();
            _game.DealPlotCards(_game.LeaderPlayerId, assignments);
        }

        private void ResolveCards()
        {
            foreach (var card in ResistancePlotCardQueries.UnplayedImmediateCards(_game).OrderBy(x => x.CardId).ToList())
            {
                var holder = card.HolderPlayerId!.Value;
                var targets = ResistancePlotCardQueries.Neighbours(_game, holder)
                    .Concat(Seats.Select(x => x.PlayerId).Where(x => x != holder));
                foreach (var target in targets)
                {
                    if (_game.PlayPlotCard(holder, card.CardId, target, Now).Failure is null)
                    {
                        break;
                    }
                }
            }

            foreach (var card in ResistancePlotCardQueries.UnconfirmedCards(_game).OrderBy(x => x.CardId).ToList())
            {
                _game.ConfirmPlotCard(ResistancePlotCardQueries.Viewer(card), card.CardId);
            }
        }

        private void Window()
        {
            var phase = _game.Phase;
            PlayWindowCards(phase);

            // Every card is played once at most, so that the game goes on
            foreach (var seat in Seats)
            {
                if (_game.Phase != phase)
                {
                    return;
                }

                _game.PassLeaderWindow(seat.PlayerId);
            }
        }

        private void PlayWindowCards(ResistancePhase phase)
        {
            foreach (var card in _game.PlotCards.Where(x => x.Location == ResistancePlotCardLocation.Hand && !_played.Contains(x.CardId))
                         .OrderBy(x => x.CardId)
                         .ToList())
            {
                if (_game.Phase != phase)
                {
                    return;
                }

                PlayWindowCard(card, phase);
            }
        }

        private void PlayWindowCard(ResistancePlotCard card, ResistancePhase phase)
        {
            var holder = card.HolderPlayerId;
            var team = Seats.Where(x => x.IsOnTeam && x.PlayerId != holder).ToList();
            switch (card.Type)
            {
                case ResistancePlotCardType.StrongLeader when phase == ResistancePhase.LeaderWindow:
                    _played.Add(card.CardId);
                    _game.TakeLead(holder, card.CardId, Now);
                    break;
                case ResistancePlotCardType.NoConfidence when phase == ResistancePhase.ConfidenceWindow:
                    _played.Add(card.CardId);
                    _game.PlayNoConfidence(holder, card.CardId, Now);
                    break;
                case ResistancePlotCardType.InTheSpotlight when phase == ResistancePhase.ConfidenceWindow:
                    _played.Add(card.CardId);
                    if (team.Count > 0)
                    {
                        _game.PlayPlotCard(holder, card.CardId, team[0].PlayerId, Now);
                    }

                    break;
                case ResistancePlotCardType.KeepingCloseEye when phase == ResistancePhase.PeekWindow:
                    _played.Add(card.CardId);
                    if (team.Count > 0)
                    {
                        _game.PeekMissionCard(holder, card.CardId, team[0].PlayerId, Now);
                    }

                    break;
            }
        }

        private List<ResistancePlotCard> HandCards(ResistancePlotCardType type)
        {
            return _game.PlotCards
                .Where(x => x.Type == type && x.Location == ResistancePlotCardLocation.Hand && !_played.Contains(x.CardId))
                .OrderBy(x => x.CardId)
                .ToList();
        }

        private void PlayMission()
        {
            foreach (var member in Seats.Where(x => x.IsOnTeam && x.MissionCard is null))
            {
                if (_game.Phase != ResistancePhase.PlayingMission)
                {
                    return;
                }

                // A spy fails when the script says so, but a module may allow only a Success (a Reverser)
                var fail = SpiesFail() && member.Role == ResistanceRole.Spy;
                var outcome = _game.PlayMissionCard(
                    member.PlayerId,
                    fail ? ResistanceMissionCardKind.Fail : ResistanceMissionCardKind.Success);
                if (outcome.Failure is not null)
                {
                    _game.PlayMissionCard(member.PlayerId, ResistanceMissionCardKind.Success);
                }
            }
        }

        private void Inquire()
        {
            var examiner = _game.InquisitionExaminerPlayerId ?? _game.InquisitorPlayerId;
            if (_game.InquisitionExaminerPlayerId is not null)
            {
                _game.ConfirmInquisition(examiner);

                return;
            }

            var target = Seats.First(x => ResistanceFlow.CanExamine(_game, examiner!.Value, x.PlayerId));
            _game.Examine(examiner, target.PlayerId);
        }

        /// <summary>The Assassin names the Commander in a game of seven and the first other seat in a game of five, so that both sides win.</summary>
        private void Assassinate()
        {
            var seats = Seats;
            var assassin = seats.First(x => x.Character == ResistanceCharacter.Assassin);
            var target = seats.Count == 7
                ? seats.First(x => x.Character == ResistanceCharacter.Commander)
                : seats.First(x => x != assassin);
            _game.Assassinate(assassin.PlayerId, target.PlayerId);
        }
    }

    [Fact]
    public void TheSnapshot_IsTheSameEveryTime()
    {
        Assert.Equal(Pinned.Value, Snapshot());
    }

    [Fact]
    public void EveryGame_IsPlayedToItsEnd()
    {
        var snapshot = Pinned.Value;

        Assert.DoesNotContain("STUCK", snapshot);
        foreach (var line in snapshot.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(x => !x.Contains("=> refused")))
        {
            Assert.Matches(@" \d+End(\{[SR]+\})? -> (Resistance|Spy)( [RS]+)?$", line);
        }
    }

    [Fact]
    public void TheSnapshot_VisitsEveryPhaseAWholeGameCanReach_AndEndsWithBothWinners()
    {
        var snapshot = Pinned.Value;

        foreach (var phase in Enum.GetValues<ResistancePhase>().Where(x => x != ResistancePhase.VoteResult))
        {
            Assert.Matches($@" \d+{Code(phase)}[ {{-]", snapshot);
        }

        Assert.Contains("-> Resistance", snapshot);
        Assert.Contains("-> Spy", snapshot);
    }

    [Fact]
    public void TheSnapshot_ShowsTheDefectorsChangeSides_AndTheInquisitionAndTheAssassinationOpen()
    {
        var snapshot = Pinned.Value;
        var flipped = snapshot
            .Split('\n')
            .Where(x => x.Contains("Defector/"))
            .Any(line => Regex.Matches(line, @"\{[SR]+\}").Select(x => x.Value).Distinct().Count() > 1);

        Assert.True(flipped, "No game has the Defectors change sides");
        Assert.Matches(@"Inquisitor/\S+ .* \d+Inq ", snapshot);
        Assert.Matches(@"Assassin/\S+ .* \d+Ass ", snapshot);
    }

    [Fact]
    public void TheFlowOfEveryGame_IsAsPinned()
    {
        GoldenFiles.AssertMatches(GoldenFile, Pinned.Value);
    }
}
