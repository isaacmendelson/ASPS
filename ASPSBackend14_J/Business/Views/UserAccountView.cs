using Common.Entities;
using Common.Enums;
using Common.Models;
//using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Views
{
    public class UserAccountView : ASItemView
    {
        public UserAccountView(UserAccount userAccount)
            : base(userAccount.Key, "u a ")
        {
            Key = userAccount.Key;
            UserKey = userAccount.UserKey;
            AccountType = userAccount.AccountType;
            IsDisabled = userAccount.IsDisabled;
            TypeName = userAccount.TypeName;
            //LoginPhoneNumber = userAccount.LoginPhoneNumber;
            Username = userAccount.UserName;
            DateCreated = userAccount.DateCreated;
            IsDisabled = userAccount.IsDisabled;
        }

        public object Id
        {
            get
            {
                return this.Key.Value;
            }
        }

        public Key UserKey { get; private set; }
        public AccountType AccountType { get; private set; }
        public bool IsDisabled { get; private set; }
        public string TypeName { get; private set; }
        public string? Username { get; private set; }
        public DateTime DateCreated { get; private set; }
        public bool IsActive { get; private set; }
    }
}
