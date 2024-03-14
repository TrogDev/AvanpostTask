/*
Возникли некоторые проблемы, пришлось вводить костыли
1) DbCreationUtility почему то создаёт базы с NotNull на всех полях, пришлось заполнять пустой строкой
2) Не совсем понял что из себя представляют property, как их отличить от обычных полей. Возможно реализация методов связанных с ними некорректна

В целом задание имеет минимум подробностей, опираясь на тесты отобразил своё видение.
*/

using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using Avanpost.Interviews.Task.Integration.Data.DbCommon;
using Avanpost.Interviews.Task.Integration.Data.DbCommon.DbModels;
using Avanpost.Interviews.Task.Integration.Data.Models;
using Avanpost.Interviews.Task.Integration.Data.Models.Models;

namespace Avanpost.Interviews.Task.Integration.SandBox.Connector
{
    public class ConnectorDb : IConnector
    {
        private DataContext context;

        public void StartUp(string connectionString)
        {
            context = new DataContext(getDbOptions(connectionString));
        }

        private DbContextOptions<DataContext> getDbOptions(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
            var connectionStringBuilder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            if (connectionStringBuilder["Provider"].ToString()!.StartsWith("PostgreSQL"))
            {
                optionsBuilder.UseNpgsql(connectionStringBuilder["ConnectionString"].ToString());
            }
            else if (connectionStringBuilder["Provider"].ToString()!.StartsWith("SqlServer"))
            {
                optionsBuilder.UseSqlServer(connectionStringBuilder["ConnectionString"].ToString());
            }
            else
            {
                throw new NotSupportedException();
            }

            return optionsBuilder.Options;
        }

        public void CreateUser(UserToCreate user)
        {
            Dictionary<string, string> properties = user.Properties.ToDictionary(
                e => e.Name,
                e => e.Value
            );

            var userEntity = new User()
            {
                Login = user.Login,
                LastName = properties.GetValueOrDefault("lastName", ""),
                FirstName = properties.GetValueOrDefault("firstName", ""),
                MiddleName = properties.GetValueOrDefault("middleName", ""),
                TelephoneNumber = properties.GetValueOrDefault("telephoneNumber", ""),
                IsLead = bool.Parse(properties.GetValueOrDefault("isLead")!)
            };

            var sequrityEntity = new Sequrity()
            {
                UserId = userEntity.Login,
                Password = user.HashPassword
            };

            context.Users.Add(userEntity);
            context.Passwords.Add(sequrityEntity);

            context.SaveChanges();
        }

        // Не совсем понятно что считать за property
        public IEnumerable<Property> GetAllProperties()
        {
            return new List<Property>()
            {
                new("firstName", null),
                new("middleName", null),
                new("lastName", null),
                new("telephoneNumber", null),
                new("password", null)
            };
        }

        public IEnumerable<UserProperty> GetUserProperties(string userLogin)
        {
            User user = context.Users.First(e => e.Login == userLogin);
            Sequrity sequrity = context.Passwords.First(e => e.UserId == userLogin);

            return new List<UserProperty>()
            {
                new("firstName", user.FirstName),
                new("middleName", user.MiddleName),
                new("lastName", user.LastName),
                new("telephoneNumber", user.TelephoneNumber),
                new("password", sequrity.Password)
            };
        }

        public bool IsUserExists(string userLogin)
        {
            return context.Users.Any(e => e.Login == userLogin);
        }

        public void UpdateUserProperties(IEnumerable<UserProperty> properties, string userLogin)
        {
            User user = context.Users.First(e => e.Login == userLogin);
            Sequrity sequrity = context.Passwords.First(e => e.UserId == userLogin);

            foreach (UserProperty property in properties)
            {
                switch (property.Name)
                {
                    case "firstName":
                        user.FirstName = property.Value;
                        break;
                    case "middleName":
                        user.MiddleName = property.Value;
                        break;
                    case "lastName":
                        user.LastName = property.Value;
                        break;
                    case "telephoneNumber":
                        user.TelephoneNumber = property.Value;
                        break;
                    case "password":
                        sequrity.Password = property.Value;
                        break;
                }
            }

            context.SaveChanges();
        }

        public IEnumerable<Permission> GetAllPermissions()
        {
            IEnumerable<Permission> rightsPermissions = context.RequestRights.Select(
                e => new Permission(e.Id.ToString()!, e.Name, "")
            );
            IEnumerable<Permission> rolePermissions = context.ITRoles.Select(
                e => new Permission(e.Id.ToString()!, e.Name, "")
            );
            return rightsPermissions.Concat(rolePermissions);
        }

        public void AddUserPermissions(string userLogin, IEnumerable<string> rightIds)
        {
            List<UserRequestRight> userRights = context
                .UserRequestRights.Where(e => e.UserId == userLogin)
                .ToList();
            List<UserITRole> userRoles = context
                .UserITRoles.Where(e => e.UserId == userLogin)
                .ToList();

            foreach (string rightId in rightIds)
            {
                int databaseId = int.Parse(rightId.Split(":")[1]);

                if (rightId.StartsWith("Role"))
                {
                    context.UserITRoles.Add(
                        new UserITRole() { UserId = userLogin, RoleId = databaseId }
                    );
                }
                else if (rightId.StartsWith("Request"))
                {
                    context.UserRequestRights.Add(
                        new UserRequestRight() { UserId = userLogin, RightId = databaseId }
                    );
                }
            }

            context.SaveChanges();
        }

        public void RemoveUserPermissions(string userLogin, IEnumerable<string> rightIds)
        {
            foreach (string rightId in rightIds)
            {
                int databaseId = int.Parse(rightId.Split(":")[1]);

                if (rightId.StartsWith("Role"))
                {
                    UserITRole userRole = context.UserITRoles.First(
                        e => e.UserId == userLogin && e.RoleId == databaseId
                    );
                    context.Remove(userRole);
                }
                else if (rightId.StartsWith("Request"))
                {
                    UserRequestRight userRight = context.UserRequestRights.First(
                        e => e.UserId == userLogin && e.RightId == databaseId
                    );
                    context.Remove(userRight);
                }
            }

            context.SaveChanges();
        }

        public IEnumerable<string> GetUserPermissions(string userLogin)
        {
            List<string> userRoles = context
                .UserRequestRights.Where(e => e.UserId == userLogin)
                .Select(e => $"Request:{e.RightId}")
                .ToList();
            List<string> userRights = context
                .UserITRoles.Where(e => e.UserId == userLogin)
                .Select(e => $"Role:{e.RoleId}")
                .ToList();
            
            return userRoles.Concat(userRights);
        }

        public ILogger Logger { get; set; }
    }
}
