using Common.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Views
{
    public class UserView : ASItemView
    {
        public UserView(User user)
            : base(user.Key, "u ")
        {
            Key = user.Key;
            FirstName = user.FirstName;
            LastName = user.LastName;
            //Email = user.Email;
            Address = user.Address;
            City = user.City;
            State = user.State;
            Country = user.Country;
            Zip = user.Zip;
            //Phone = user.Phone;

        }

        public string? FirstName { get; private set; }
        public string LastName { get; private set; }
        public string? Email { get; private set; }
        public string? Address { get; private set; }
        public string? City { get; private set; }
        public string? State { get; private set; }
        public string Country { get; private set; }
        public string? Zip { get; private set; }
        public string? Phone { get; private set; }
    }
}
