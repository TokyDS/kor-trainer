namespace KorTrainer.Contracts;

/// <param name="MachineTranslated">The Russian was machine-translated (the Korean is from Tatoeba).</param>
public sealed record PracticeSentenceDto(int Id, string Korean, string Russian, bool MachineTranslated, string Author);

/// <param name="Type">dictation or shadowing.</param>
/// <param name="FromStudiedWords">Every sentence is made of words the learner has already studied.</param>
public sealed record SentenceSetDto(string Type, string Title, bool FromStudiedWords, IReadOnlyList<PracticeSentenceDto> Items);
