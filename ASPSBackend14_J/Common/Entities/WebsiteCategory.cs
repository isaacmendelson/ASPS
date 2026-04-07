#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Serialization;
using Common.Enums;
using Common.Models;

namespace Common.Entities
{
    [DataContract]
    public class WebsiteCategory : Entity
    {
        protected WebsiteCategory()
        {
        }

        public WebsiteCategory(string name, string parentId, string? source)
        {
            //Value = value;
            Name = name;
            ParentId = parentId;
            DateCreated = DateTime.UtcNow;
            Source = source;
        }

        public WebsiteCategory(string name, int parentName, string? source)
        {
            Name = name;
            DateCreated = DateTime.UtcNow;
            Source = source;
        }

        //[DataMember]
        //public int Value { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember] 
        public string ParentId { get; set; }

        [NotMapped]
        [ForeignKey(nameof(ParentId))]  
        public WebsiteCategory? Parent { get; set; }

        [DataMember] 
        public DateTime DateCreated { get; set; }

        [DataMember] 
        public DateTime? DateDeleted { get; set; }

        [DataMember] 
        public string? Source { get; set; }

        public override string TypeName { 
            get
            { 
                return nameof(WebsiteCategory);
            }
        }

        public override Tag Tag
        {
            get
            {
                return new Tag(new Key(this.TypeName, this.KeyField), this.Name, this.Parent is not null ? this.Parent.Name : this.TypeName);
            }
        }

        public bool IsDeleted
        {
            get
            {
                return this.DateDeleted is not null;
            }
        }
    }
}
