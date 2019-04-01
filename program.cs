using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema;
namespace SoloLearn
{
    class Person {
        public string FirstName{get;set;}
        
        public string LastName{get;set;}
    }
    
	class Program
	{
		static void Main(string[] args)
		{		    
		    //Examples:
		    //executes ExecuteNonQueryAsync()
		    var executeTask = Task.Run(async () =>
                {
                    await SqlHelper.ExecuteAsync(connectionString:"ConnectionString",
                    command:"Sql command", CommandType.Text, sqlParams:new { Id = 5 });
                });

                executeTask.Wait();
            
            //executes ExecuteReaderAsync()    
             var readerTask = Task.Run(async () =>
                {
                    var persons = await SqlHelper.ExecuteReaderAsync<List<Person>>(connectionString:"ConnectionString",
                    command:"SELECT FirstName,LastName FROM Persons", CommandType.Text);
                });

                readerTask.Wait();
		}
	}	
}