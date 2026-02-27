using Common.Entities;
using Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Models
{
    public class UserInfo
    {
        public UserInfo(Key key, string keycloakUserId, string firstName, string lastName, string address, string city, string state, string zip, string country, 
            string phoneNumber, UserRole role, bool isDisabled, DateTime dateCreated, int? guardianKey, string? locale, int? timezone)
        {
            KeycloakUserId = keycloakUserId;
            FirstName = firstName;
            LastName = lastName;
            Address = address;
            City = city;
            State = state;
            Zip = zip;
            Country = country;
            PhoneNumber = phoneNumber;
            Role = role;
            GuardianKey = guardianKey;
            Locale = locale;
            Timezone = timezone;
            DateCreated = dateCreated;
            IsDisabled = isDisabled;
            Key = key;
        }

        public UserInfo(User user)
        {
            Key = user.Key;
            KeycloakUserId = user.KeycloakUserId;
            FirstName = user.FirstName;
            LastName = user.LastName;
            Address = user.Address;
            City = user.City;
            State = user.State;
            Zip = user.Zip;
            Country = user.Country;
            PhoneNumber = user.PhoneNumber;
            Role = user.Role;
            GuardianKey = user.GuardianKey;
            Locale = user.Locale;
            Timezone = user.Timezone;
            DateCreated = user.DateCreated;
            DateModified = user.DateModified;
            DateDeleted = user.DateDeleted;
            IsDisabled = user.IsDisabled;
        }
        public Key Key{ get; set; } 
        public string KeycloakUserId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public int? GuardianKey { get; set; }
        public string? Locale { get; set; }
        public int? Timezone { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? DateModified { get; set; }
        public DateTime? DateDeleted { get; set; }
        public bool IsDisabled { get; set; }

        // Get full name
        public string FullName => $"{this.FirstName} {this.LastName}".Trim();
    }
}
