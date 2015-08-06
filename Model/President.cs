using System.Linq;
using Microsoft.Azure.Search.Models;

namespace Model
{
    // Azure Search uses camel case, that attributes makes Pascal case compatible with it
    // Azure Search utilises camel case, cet attribut rend Pascal case compatible avec
    [SerializePropertyNamesAsCamelCase]
    public class President
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Party { get; set; }

        public string RawTerms { get; set; }

        // Rawterm is of structure "DATE-DATE", so split with '-' and get the first part
        // Rawterm a une structure "DATE-DATE", donc splitter avec '-' et prendre la première partie
        public int TermStart => int.Parse(RawTerms.Split('-')[0]);
        // Take second part.
        // Prend la seconde partie
        public int? TermEnd
        {
            get
            {
                var data = RawTerms.Split('-');

                // Exception ! One president died few month after term start, so date is format "DATE". So take first
                // Exception ! Une président est mort quelques mois après son inoguration, donc la date est "DATE". Donc il faut prendre la 1ere partie
                if (data.Count() == 1)
                {
                    return int.Parse(data[0]);
                }
                // For surrent president second part is null
                // Pour le président actuel, la deuxième partie de la date est null
                return !string.IsNullOrEmpty(data[1]) ? int.Parse(data[1]) as int? : null;

            }
        }
    }
}
