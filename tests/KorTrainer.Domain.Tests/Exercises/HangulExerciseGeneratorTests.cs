using AwesomeAssertions;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Exercises;
using KorTrainer.Domain.Hangul;

namespace KorTrainer.Domain.Tests.Exercises;

public sealed class HangulExerciseGeneratorTests
{
    private const int SampleSize = 200;

    public static TheoryData<string, HangulLevel> AllTypes => new()
    {
        { ExerciseTypes.LetterToSound, HangulLevel.Basics },
        { ExerciseTypes.LetterToSound, HangulLevel.Full },
        { ExerciseTypes.SoundToLetter, HangulLevel.Basics },
        { ExerciseTypes.SoundToLetter, HangulLevel.Full },
        { ExerciseTypes.Compose, HangulLevel.Basics },
        { ExerciseTypes.Compose, HangulLevel.Batchim },
        { ExerciseTypes.Decompose, HangulLevel.Basics },
        { ExerciseTypes.Decompose, HangulLevel.Batchim },
        { ExerciseTypes.ConsonantSeries, HangulLevel.Full },
        { ExerciseTypes.FinalSound, HangulLevel.Batchim },
        { ExerciseTypes.SoundChange, HangulLevel.Batchim },
    };

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void Generate_ProducesWellFormedQuestions(string type, HangulLevel level)
    {
        var generator = new HangulExerciseGenerator(new Random(42));

        var exercises = generator.Generate(type, level, SampleSize);

        exercises.Should().HaveCount(SampleSize);
        foreach (var exercise in exercises)
        {
            exercise.Type.Should().Be(type);
            exercise.Options.Count.Should().BeInRange(2, HangulExerciseGenerator.OptionCount);
            exercise.Options.Select(o => o.Text).Should().OnlyHaveUniqueItems();
            exercise.CorrectIndex.Should().BeInRange(0, exercise.Options.Count - 1);
            exercise.Explanation.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Generate_IsDeterministicForTheSameSeed()
    {
        var first = new HangulExerciseGenerator(new Random(7)).Generate(ExerciseTypes.Compose, HangulLevel.Batchim, 20);
        var second = new HangulExerciseGenerator(new Random(7)).Generate(ExerciseTypes.Compose, HangulLevel.Batchim, 20);

        first.Select(e => e.ItemKey).Should().Equal(second.Select(e => e.ItemKey));
    }

    [Fact]
    public void LetterToSound_AtBasicsLevel_UsesOnlyBasicLetters()
    {
        var basics = JamoCatalog.ForLevel(HangulLevel.Basics).Select(j => j.Jamo.ToString()).ToHashSet();
        var generator = new HangulExerciseGenerator(new Random(1));

        var prompts = generator.Generate(ExerciseTypes.LetterToSound, HangulLevel.Basics, SampleSize).Select(e => e.Prompt);

        prompts.Should().OnlyContain(p => basics.Contains(p));
    }

    [Fact]
    public void LetterToSound_NeverOffersTwoLettersThatSoundAlike()
    {
        var generator = new HangulExerciseGenerator(new Random(3));

        foreach (var exercise in generator.Generate(ExerciseTypes.SoundToLetter, HangulLevel.Full, SampleSize))
        {
            var groups = exercise.Options.Select(o => JamoCatalog.Get(o.Text[0]).SoundGroup);
            groups.Should().OnlyHaveUniqueItems();
        }
    }

    [Fact]
    public void Compose_CorrectAnswerIsTheComposedSyllable()
    {
        var generator = new HangulExerciseGenerator(new Random(5));

        foreach (var exercise in generator.Generate(ExerciseTypes.Compose, HangulLevel.Batchim, SampleSize))
        {
            var jamo = exercise.Prompt.Split(" + ").Select(s => s[0]).ToArray();
            var expected = new Syllable(jamo[0], jamo[1], jamo.Length == 3 ? jamo[2] : null).ToString();
            exercise.Correct.Text.Should().Be(expected);
        }
    }

    [Fact]
    public void Compose_MostlyUsesSyllablesOfRealWords()
    {
        string[] words = ["나무", "바다", "어머니", "아버지", "가수", "모자", "누나", "다리", "머리", "아주", "저기", "거기"];
        var real = words.SelectMany(w => w).ToHashSet();
        var generator = new HangulExerciseGenerator(new Random(29));

        var exercises = generator.Generate(ExerciseTypes.Compose, HangulLevel.Basics, SampleSize, words);

        exercises.Count(e => real.Contains(e.Correct.Text[0])).Should().BeGreaterThan(SampleSize / 2);
    }

    [Fact]
    public void Decompose_CorrectAnswerListsTheJamoOfThePrompt()
    {
        var generator = new HangulExerciseGenerator(new Random(9));

        foreach (var exercise in generator.Generate(ExerciseTypes.Decompose, HangulLevel.Batchim, SampleSize))
        {
            exercise.Correct.Text.Should().Be(string.Join(" + ", Syllable.Decompose(exercise.Prompt[0]).Jamo));
        }
    }

    [Fact]
    public void Compose_BelowBatchimLevel_HasNoFinals()
    {
        var generator = new HangulExerciseGenerator(new Random(11));

        var answers = generator.Generate(ExerciseTypes.Compose, HangulLevel.Full, SampleSize).SelectMany(e => e.Options);

        answers.Should().OnlyContain(o => !Batchim.HasBatchim(o.Text[0]));
    }

    [Fact]
    public void ConsonantSeries_KeepsPlainAspiratedTenseOrderAndSpeaksTheAnswer()
    {
        var generator = new HangulExerciseGenerator(new Random(13));

        foreach (var exercise in generator.Generate(ExerciseTypes.ConsonantSeries, HangulLevel.Full, SampleSize))
        {
            exercise.AutoSpeak.Should().BeTrue();
            exercise.SpeakText.Should().Be(exercise.Correct.Text);
            exercise.Options[0].Hint.Should().StartWith("обычная");
            exercise.Options.Should().OnlyContain(o => MinimalPairs.Sets.SelectMany(s => s).Any(w => w.Korean == o.Text));
        }
    }

    [Fact]
    public void MinimalPairs_DifferOnlyInTheInitialConsonantOfOneSeries()
    {
        foreach (var set in MinimalPairs.Sets)
        {
            set.Count.Should().BeInRange(2, 3);
            var first = set[0];
            foreach (var word in set)
            {
                var syllable = Syllable.Decompose(word.Korean[0]);
                var reference = Syllable.Decompose(first.Korean[0]);
                word.Korean.Length.Should().Be(first.Korean.Length);
                word.Korean[1..].Should().Be(first.Korean[1..], "only the first consonant may differ");
                syllable.Medial.Should().Be(reference.Medial);
                syllable.Final.Should().Be(reference.Final);
            }

            var series = JamoCatalog.ConsonantSeries.Single(s => s.Contains(first.Consonant));
            set.Select(w => series.ToList().IndexOf(w.Consonant)).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems().And.NotContain(-1);
        }
    }

    [Fact]
    public void FinalSound_AnswerIsTheRepresentativeSound()
    {
        var generator = new HangulExerciseGenerator(new Random(17));

        foreach (var exercise in generator.Generate(ExerciseTypes.FinalSound, HangulLevel.Batchim, SampleSize))
        {
            char final = Syllable.Decompose(exercise.Prompt[0]).Final!.Value;
            exercise.Correct.Text.Should().Be(Pronunciation.RepresentativeSound(final).ToString());
        }
    }

    [Fact]
    public void SoundChange_AnswersWithPronunciationAndOffersTheSpelling()
    {
        var generator = new HangulExerciseGenerator(new Random(19));

        var exercise = generator.SoundChange(["한국어"]);

        exercise.Correct.Text.Should().Be($"[{Pronunciation.Pronounce(exercise.Prompt).Text}]");
        exercise.Options.Should().Contain(o => o.Text == $"[{exercise.Prompt}]");
    }

    [Theory]
    [InlineData("나무", HangulLevel.Basics, true)]
    [InlineData("우유", HangulLevel.Basics, false)]
    [InlineData("우유", HangulLevel.Full, true)]
    [InlineData("학교", HangulLevel.Full, false)]
    [InlineData("학교", HangulLevel.Batchim, true)]
    [InlineData("안녕 하세요", HangulLevel.Batchim, true)]
    [InlineData("TV", HangulLevel.Batchim, false)]
    public void IsReadableAt_ChecksLettersAndFinalsOfTheLevel(string text, HangulLevel level, bool expected) =>
        HangulExerciseGenerator.IsReadableAt(text, level).Should().Be(expected);

    [Fact]
    public void Reading_FillsWithPseudoWordsWhenDeckIsTooSmall()
    {
        var generator = new HangulExerciseGenerator(new Random(23));

        var items = generator.Reading(HangulLevel.Basics, 5, [("나무", "дерево"), ("학교", "школа")]);

        items.Should().HaveCount(5);
        items.Should().ContainSingle(i => i.Text == "나무").Which.Translation.Should().Be("дерево");
        items.Should().NotContain(i => i.Text == "학교");
        items.Should().OnlyContain(i => HangulExerciseGenerator.IsReadableAt(i.Text, HangulLevel.Basics));
    }

    [Fact]
    public void Generate_RejectsUnknownType()
    {
        var act = () => new HangulExerciseGenerator(new Random(1)).Generate("nope", HangulLevel.Basics, 1);

        act.Should().Throw<ArgumentException>();
    }
}
