using KorTrainer.Domain.Common;
using static KorTrainer.Domain.Common.ExerciseTypes;

namespace KorTrainer.Domain.Plan;

/// <summary>Tasks shared by several stages.</summary>
internal static class CommonTasks
{
    public static PlanTask ReviewDue { get; } =
        new("vocab-due", "Повторить все слова к повторению", new TaskTarget(TaskMetric.VocabularyDueCleared), "/vocab/review");

    public static PlanTask NewWords(int count) =>
        new("vocab-new", $"Выучить новые слова: {count}", new TaskTarget(TaskMetric.NewWords, count), "/vocab/review");

    public static PlanTask Reading(int count, int level = 3) =>
        new("reading", $"Чтение на время: {count} слов", new TaskTarget(TaskMetric.ReadingTrials, count), $"/hangul/reading?level={level}");
}

/// <summary>Russian learning content of the plan: stage names, lesson notes and lesson tasks.</summary>
internal static class PlanContent
{
    public static IReadOnlyList<LessonInfo> Lessons { get; } =
    [
        new(1, "Хангыль: базовые гласные и согласные, слоговой блок",
        [
            "Слог — это «квадратик»: согласная + гласная, иногда снизу ещё согласная (патчим).",
            "Вертикальные гласные (ㅏ ㅓ ㅣ ㅐ ㅔ) пишутся справа от согласной, горизонтальные (ㅗ ㅜ ㅡ) — под ней.",
            "ㅇ в начале слога не читается: 아 = «а», 오 = «о».",
            "ㄱ ㄷ ㅂ ㅈ в начале слова звучат глуше (к, т, п, ч), между гласными — звонко (г, д, б, дж).",
            "ㅓ и ㅗ — две разные «о»: открытая и с губами трубочкой.",
        ]),
        new(2, "Хангыль: йотированные и составные гласные, три ряда согласных",
        [
            "Второй штрих добавляет «й»: ㅏ→ㅑ, ㅓ→ㅕ, ㅗ→ㅛ, ㅜ→ㅠ.",
            "Составные гласные сливают две: ㅗ+ㅏ=ㅘ, ㅜ+ㅓ=ㅝ, ㅡ+ㅣ=ㅢ.",
            "Три ряда согласных: обычная (ㄱ), с придыханием (ㅋ — сильный выдох), напряжённая (ㄲ — без выдоха, «твёрдо»).",
            "Проверка ладонью у рта: на ㅋ ㅌ ㅍ ㅊ чувствуется поток воздуха, на ㄲ ㄸ ㅃ ㅉ — нет.",
            "ㅐ и ㅔ в современной речи звучат почти одинаково — различай их по написанию.",
        ]),
        new(3, "Хангыль: патчим (7 конечных звуков) и связывание 연음",
        [
            "Патчимом может стоять любая из 27 комбинаций, но звучат только 7 звуков: ㄱ ㄴ ㄷ ㄹ ㅁ ㅂ ㅇ.",
            "ㅋ ㄲ → ㄱ; ㅅ ㅆ ㅈ ㅊ ㅌ ㅎ → ㄷ; ㅍ → ㅂ. Например, 꽃 читается [꼳].",
            "연음: если следующий слог начинается с ㅇ, патчим «переезжает» туда: 한국어 → [한구거], 음악 → [으막].",
            "Двойной патчим перед гласной: первый остаётся, второй переходит: 읽어요 → [일거요].",
            "ㅎ перед гласной пропадает: 좋아요 → [조아요].",
        ]),
        new(4, "Приветствия и вежливость",
        [
            "안녕하세요 — универсальное «здравствуйте».",
            "Уходящему говорят 안녕히 가세요, остающемуся — 안녕히 계세요.",
            "감사합니다 / 고마워요 — «спасибо» (официально / вежливо-нейтрально).",
            "죄송합니다 / 미안해요 — «извините».",
            "네 — да, 아니요 — нет.",
        ]),
        new(5, "Связка 이에요/예요: «это …»",
        [
            "이에요 — после согласной (есть патчим): 책이에요 — «это книга».",
            "예요 — после гласной (нет патчима): 의자예요 — «это стул».",
            "Вопрос — та же форма с вопросительной интонацией: 학생이에요?",
            "Как понять: в последнем слоге 책 внизу стоит ㄱ → патчим есть → 이에요.",
        ]),
        new(6, "Отрицание 이/가 아니에요 и вопрос 이게 뭐예요?",
        [
            "N이/가 아니에요 — «это не N».",
            "이 — после согласной, 가 — после гласной: 책이 아니에요, 의자가 아니에요.",
            "이게 뭐예요? — «Что это?»; 그게 — то (у собеседника), 저게 — вон то (далеко от обоих).",
            "이것 / 그것 / 저것 — полные формы «это / то / вон то».",
        ]),
        new(7, "Сино-корейские числа",
        [
            "일 이 삼 사 오 육 칠 팔 구 십.",
            "11 = 십일, 20 = 이십, 99 = 구십구, 100 = 백, 1000 = 천, 10 000 = 만.",
            "Перед 백, 천, 만 «один» не говорят: 백, 천, 만 (а не 일백).",
            "Ими называют даты, деньги, номера, минуты: 삼월 이십일, 만 원, 오 분.",
            "Исключения: 6월 = 유월, 10월 = 시월.",
        ]),
        new(8, "Корейские числа и счётные слова",
        [
            "하나 둘 셋 넷 다섯 여섯 일곱 여덟 아홉 열, 20 — 스물.",
            "Перед счётным словом 하나/둘/셋/넷/스물 сокращаются: 한, 두, 세, 네, 스무 — 사과 한 개, 학생 두 명.",
            "Счётные слова: 개 (штуки), 명 (люди), 살 (возраст), 시 (час), 권 (книги), 잔 (чашки), 마리 (животные).",
            "Часы — корейскими числами, минуты — сино-корейскими: 세 시 십 분.",
        ]),
        new(9, "Самопредставление",
        [
            "저는 … 이에요/예요 — «Я …»; 은/는 — частица темы.",
            "은 — после согласной, 는 — после гласной: 저는, 선생님은.",
            "저는 러시아 사람이에요. 저는 학생이에요.",
            "이름이 뭐예요? — «Как вас зовут?»; 만나서 반가워요 — «Приятно познакомиться».",
        ]),
        new(10, "Повторение первых фраз",
        [
            "Повтори все слова, которые пришли на повторение.",
            "Смешанная тренировка: связка, отрицание, частицы.",
            "Числа обеих систем, время и счётные слова.",
        ]),
    ];

    public static IReadOnlyList<PlanTask> LessonTasks(int day, int newWordsPerDay) => day switch
    {
        1 =>
        [
            Guide(1),
            Hangul(LetterToSound, "Буква → звук", 20, 1),
            Hangul(SoundToLetter, "Звук → буква", 20, 1),
            Hangul(Compose, "Сборка слога из букв", 15, 1),
            Hangul(Decompose, "Разбор слога на буквы", 15, 1),
            CommonTasks.Reading(10, 1),
        ],
        2 =>
        [
            Guide(2),
            Hangul(LetterToSound, "Буква → звук", 20, 2),
            Hangul(SoundToLetter, "Звук → буква", 20, 2),
            Hangul(ConsonantSeries, "Три ряда согласных на слух", 30, 2),
            CommonTasks.Reading(10, 2),
        ],
        3 =>
        [
            Guide(3),
            Hangul(FinalSound, "Как звучит патчим", 20, 3),
            Hangul(SoundChange, "Связывание 연음 и чтение по правилам", 15, 3),
            Hangul(Decompose, "Разбор слогов с патчимом", 15, 3),
            CommonTasks.Reading(15, 3),
        ],
        4 =>
        [
            CommonTasks.NewWords(newWordsPerDay),
            CommonTasks.ReviewDue,
            new("say-aloud", "Произнести вслух все приветствия урока по 3 раза", TaskTarget.Manual()),
            CommonTasks.Reading(10),
        ],
        5 =>
        [
            CommonTasks.NewWords(newWordsPerDay),
            CommonTasks.ReviewDue,
            Grammar(Copula, "이에요/예요", 20),
        ],
        6 =>
        [
            CommonTasks.NewWords(newWordsPerDay),
            CommonTasks.ReviewDue,
            Grammar(NegativeCopula, "이/가 아니에요", 20),
        ],
        7 =>
        [
            CommonTasks.NewWords(newWordsPerDay),
            CommonTasks.ReviewDue,
            Numbers(SinoNumbers, "Сино-корейские числа", 20),
        ],
        8 =>
        [
            CommonTasks.NewWords(newWordsPerDay),
            CommonTasks.ReviewDue,
            Numbers(NativeNumbers, "Корейские числа", 20),
            Numbers(Counters, "Счётные слова", 15),
        ],
        9 =>
        [
            CommonTasks.NewWords(newWordsPerDay),
            CommonTasks.ReviewDue,
            Grammar(TopicParticle, "은/는", 20),
            new("self-intro", "Написать 5 предложений о себе и прочитать их вслух", TaskTarget.Manual()),
        ],
        10 =>
        [
            CommonTasks.ReviewDue,
            new("grammar", "Грамматика вперемешку: 20 упражнений", TaskTarget.Answers(StudyModule.Grammar, 20), "/grammar/mixed"),
            new("numbers", "Числа вперемешку: 20 упражнений", TaskTarget.Answers(StudyModule.Numbers, 20), "/numbers/mixed"),
            CommonTasks.Reading(15),
        ],
        _ => [],
    };

    public static IReadOnlyList<QuickLink> QuickLinks(PlanStage stage, int day) => stage switch
    {
        PlanStage.NotStarted => [new("Памятка по хангылю", "/hangul/guide?level=1")],
        PlanStage.Hangul =>
        [
            new("Памятка", $"/hangul/guide?level={day}"),
            new("Буква → звук", $"/hangul/{LetterToSound}?level={day}"),
            new("Звук → буква", $"/hangul/{SoundToLetter}?level={day}"),
            new("Сборка слога", $"/hangul/{Compose}?level={day}"),
            new("Три ряда согласных", "/hangul/series"),
            new("Чтение на время", $"/hangul/reading?level={day}"),
            new("Раскладка клавиатуры", "/typing/1"),
        ],
        PlanStage.TopikPrep or PlanStage.MockExam =>
        [
            new("Повторение слов", "/vocab/review"),
            new("TOPIK I: чтение", "/topik/reading/mixed"),
            new("Письмо 51–54", "/topik/writing"),
            new("Тексты", "/read"),
            new("Диктант", "/listening/dictation"),
            new("Трудные слова", "/vocab/hard"),
        ],
        _ =>
        [
            new("Повторение слов", "/vocab/review"),
            new("Грамматика", "/grammar"),
            new("Числа", "/numbers"),
            new("Тексты", "/read"),
            new("Диктант", "/listening/dictation"),
            new("Повтори вслух", "/listening/shadowing"),
            new("Корни ханча", "/hanja"),
            new("Трудные слова", "/vocab/hard"),
            new("TOPIK I: чтение", "/topik/reading/mixed"),
        ],
    };

    public static string StageTitle(PlanStage stage) => stage switch
    {
        PlanStage.NotStarted => "До старта",
        PlanStage.Hangul => "Хангыль",
        PlanStage.FirstPhrases => "Первые фразы",
        PlanStage.Level1 => "Этап 1: уровень 1",
        PlanStage.Level2 => "Этап 2: уровень 2",
        PlanStage.MockExam => "Пробный экзамен",
        PlanStage.TopikPrep => "Этап 3: подготовка к TOPIK II",
        PlanStage.Maintenance => "Поддержание",
        _ => stage.ToString(),
    };

    public static string StageGoal(PlanStage stage) => stage switch
    {
        PlanStage.NotStarted => "Начните с первого урока.",
        PlanStage.Hangul => "Читать любой слог без подсказок.",
        PlanStage.FirstPhrases => "Приветствия, «это …», числа, рассказ о себе.",
        PlanStage.Level1 => "Уровень 1, около 800 слов.",
        PlanStage.Level2 => "Уровень 2: соединительные окончания, -고 싶다, -(으)ㄹ 수 있다, -아/어야 하다, -습니다; 1500–2000 слов.",
        PlanStage.MockExam => "Пробный экзамен и решение: TOPIK I или TOPIK II.",
        PlanStage.TopikPrep => "TOPIK II: письмо 51–54, чтение, аудирование.",
        PlanStage.Maintenance => "Не терять форму: повторения и грамматика.",
        _ => string.Empty,
    };

    private static PlanTask Guide(int level) =>
        new("guide", "Прочитать памятку урока и прослушать буквы", TaskTarget.Manual(), $"/hangul/guide?level={level}");

    private static PlanTask Hangul(string type, string title, int count, int level) =>
        new(type, $"{title}: {count}", TaskTarget.Answers(StudyModule.Hangul, count, type), $"/hangul/{type}?level={level}");

    private static PlanTask Grammar(string type, string title, int count) =>
        new(type, $"Грамматика {title}: {count} упражнений", TaskTarget.Answers(StudyModule.Grammar, count, type), $"/grammar/{type}");

    private static PlanTask Numbers(string type, string title, int count) =>
        new(type, $"{title}: {count} упражнений", TaskTarget.Answers(StudyModule.Numbers, count, type), $"/numbers/{type}");
}
