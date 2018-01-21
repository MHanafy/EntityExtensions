# EntityExtensions
The main purpose of this project is, to provide bulk insert/update/delete support; and expose EF metadata in a easily accessible manner.

I searched a lot for a free, easy to use EF extensions library for use in one of the projects, I couldn't find any!
So, After doing it, I'm happy to share it for others to use it as well.

Initially, the project only supports bulk inserts/update/delete to SQL server, utilizing SQLBulkCopy; along with other useful atomic operations.

The plan is, to keep updating it with other vendors support (Oracle, MySql, etc...) if it gets enough demand/contribution from the community.

Using the library is as easy as adding a using statement for EntityExtensions; then using the extension methods from your DbContext.

Examples:
Bulk insert/updates/deletes

~~~csharp
var insertsAndupdates = new List<object>();
var deletes = new List<object>();
context.BulkUpdate(insertsAndupdates, deletes);
~~~
  
Direct delete by property
~~~csharp
context.DirectDeleteByProperty<Object>("PropertyName", "PropertyValue");
~~~
  
Update or insert in one go
~~~csharp
context.InsertOrUpdate(new object());
~~~

Get a map of DB column name and object property info
~~~csharp
context.GetTableColumns<object>();
~~~
  
The list goes on, And hopefully, it will get bigger over time.
