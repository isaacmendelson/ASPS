using Common.Entities;
using Common.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Business.Views
{
    public class WebsiteCategoryView : ASItemView
    {

        protected WebsiteCategoryView()
        {
        }

        public WebsiteCategoryView(WebsiteCategory wc)
        {
            //Value = value;
            this.Name = wc.Name;
            this.ParentId = wc.ParentId;
            this.DateCreated = wc.DateCreated;
            this.Source = wc.Source;
            this.Parent = wc.Parent is not null ? new WebsiteCategoryView(wc.Parent) : null;
        }

        //public WebsiteCategoryView(string name, WebsiteCategoryView? parent, string? source)
        //{
        //    Name = name;
        //    DateCreated = DateTime.UtcNow;
        //    Source = source;
        //    this.Parent = parent;
        //}

        public WebsiteCategoryView(string name, string parentName, string? source)
        {
            Name = name;
            DateCreated = DateTime.UtcNow;
            Source = source;
            this.Parent = new WebsiteCategoryView() { Name = parentName };
        }


        [DataMember]
        string Name { get; set; }

        [DataMember]
        public WebsiteCategoryView? Parent { get; set; }

        [DataMember]
        public string? ParentId { get; set; }

        [DataMember]
        public DateTime DateCreated { get; set; }
        
        [DataMember]
        public string? Source { get; set; }

        [DataMember]
        public string TypeName
        {
            get
            {
                return nameof(WebsiteCategoryView);
            }
        }

        [DataMember]
        public Key Key { get; set; }

        [DataMember]
        public Tag Tag {
            get
            {
                return new Tag(this.Key, this.Name, this.Parent?.Name ?? this.TypeName);
            }
        }

    }
}
