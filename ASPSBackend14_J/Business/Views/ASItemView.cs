using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Views
{
    public class ASItemView
    {
        protected ASItemView() { }

        public ASItemView(Tag tag)
        {
            Tag = tag;
            Key = tag.Key;
            Name = Tag.Name;
        }

        public ASItemView(Key key, string? name)
        {
            name = name ?? string.Empty;
            Tag = new Tag(key, name, "");
            Key = key;
            Name = name;
        }

        public Key Key { get; set; }
        public Tag Tag { get; private set; }
        public string Name  { get; private set; }
}
}
