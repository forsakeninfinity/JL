namespace JL.Core.Dicts.EDICT.JMdict;

internal ref struct JmdictEntry
{
    public int Id { get; set; }
    public List<KanjiElement> KanjiElements { get; }
    public List<ReadingElement> ReadingElements { get; }
    public List<Sense> SenseList { get; }

    public JmdictEntry()
    {
        Id = 0;
        KanjiElements = new List<KanjiElement>();
        ReadingElements = new List<ReadingElement>();
        SenseList = new List<Sense>();
    }
}
