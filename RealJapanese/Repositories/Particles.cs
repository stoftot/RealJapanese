namespace Repositories;

public class Particles
{
     public class Particle
    {
        public string Kana { get; set; }         // e.g. "は"
        public string Romaji { get; set; }       // e.g. "wa"
        public string Description { get; set; }  // e.g. explanation in English
    }
     
    public static readonly List<Particle> All = new()
    {
        // Topic / Subject
        new Particle { Kana = "は", Romaji = "wa", Description = "Marks the topic of the sentence (what the sentence is about). Often contrasts with が." },
        new Particle { Kana = "が", Romaji = "ga", Description = "Marks the subject that performs the action or the thing that exists or is identified." },

        // Object / Direction / Destination
        new Particle { Kana = "を", Romaji = "wo / o", Description = "Marks the direct object of a verb — the thing receiving the action." },
        new Particle { Kana = "に", Romaji = "ni", Description = "Indicates direction (to), time (at/on), purpose, or indirect object (to/for)." },
        new Particle { Kana = "へ", Romaji = "e", Description = "Shows direction or destination (toward). Similar to に but emphasizes movement." },
        new Particle { Kana = "で", Romaji = "de", Description = "Marks the location where an action takes place or the means by which something is done." },

        // Connections / Relationships
        new Particle { Kana = "と", Romaji = "to", Description = "Means 'and' when connecting nouns, or 'with' when indicating a companion or quote." },
        new Particle { Kana = "の", Romaji = "no", Description = "Shows possession ('of') or acts as a connector between two nouns ('noun of noun')." },

        // Comparisons / Additions
        new Particle { Kana = "も", Romaji = "mo", Description = "Means 'also' or 'too', replacing は, が, or を when used." },
        new Particle { Kana = "より", Romaji = "yori", Description = "Used for comparisons; means 'than' (AよりB = B than A)." },
        new Particle { Kana = "まで", Romaji = "made", Description = "Means 'until' (time) or 'up to' (place/extent)." },
        new Particle { Kana = "から", Romaji = "kara", Description = "Means 'from' (time/place) or indicates reason/cause ('because')." },
        new Particle { Kana = "だけ", Romaji = "dake", Description = "Means 'only' or 'just'." },
        new Particle { Kana = "しか", Romaji = "shika", Description = "Means 'only' but used with a negative verb (e.g., これしかない = there's only this)." },
        new Particle { Kana = "ぐらい", Romaji = "gurai", Description = "Means 'about' or 'approximately' (for amount/time)." },
        new Particle { Kana = "くらい", Romaji = "kurai", Description = "Same as ぐらい — interchangeable in most cases." },

        // Focus / Emphasis
        new Particle { Kana = "こそ", Romaji = "koso", Description = "Emphasizes the preceding word ('this very' or 'exactly this')." },
        new Particle { Kana = "でも", Romaji = "demo", Description = "Means 'even' or 'but', depending on context." },
        new Particle { Kana = "までに", Romaji = "madeni", Description = "Means 'by (a certain time)' — emphasizes deadline." },

        // Questions / Exclamations
        new Particle { Kana = "か", Romaji = "ka", Description = "Marks a question, or means 'or' when between options." },
        new Particle { Kana = "ね", Romaji = "ne", Description = "Used at the end of a sentence to seek agreement ('isn't it?', 'right?')." },
        new Particle { Kana = "よ", Romaji = "yo", Description = "Used at the end of a sentence for emphasis or assertion ('you know!')." },
        new Particle { Kana = "かな", Romaji = "kana", Description = "Used at the end of a sentence to express wonder or uncertainty ('I wonder...')." },

        // Compound particles / Others
        new Particle { Kana = "について", Romaji = "ni tsuite", Description = "Means 'about' or 'concerning'." },
        new Particle { Kana = "として", Romaji = "toshite", Description = "Means 'as' or 'in the capacity of'." },
        new Particle { Kana = "によって", Romaji = "ni yotte", Description = "Means 'by' or 'depending on' — shows agent or means." },
        new Particle { Kana = "のために", Romaji = "no tame ni", Description = "Means 'for the sake of' or 'because of'." },
        new Particle { Kana = "にとって", Romaji = "ni totte", Description = "Means 'for' (from the point of view of someone)." },
        new Particle { Kana = "など", Romaji = "nado", Description = "Means 'etc.' or 'and so on'." },
        new Particle { Kana = "ばかり", Romaji = "bakari", Description = "Means 'only', 'just', or 'nothing but' (often emphasizes repetition or excess)." }
    };
}