using System;
using System.Collections.Generic;
using System.Data.Entity.Design.PluralizationServices;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MongoDB
{
    using MongoDB.Bson;
    using MongoDB.Bson.Serialization;
    using MongoDB.Driver;

    /// <summary>
    /// Main class for Dapper.SimpleCRUD extensions
    /// </summary>
    public class SimpleCRUD
    {
        MongoClient _client;
        IMongoDatabase _db;

        #region Constructor

        public SimpleCRUD(string connectionString, string database)
        {
            if (string.IsNullOrEmpty(connectionString)) { connectionString = "mongodb://localhost:27017"; }
            _client = new MongoClient(connectionString);
            _db = _client.GetDatabase(database);
        }
        public SimpleCRUD(string database) : this(null, database) { }

        #endregion

        #region Get

        /// <summary>
        /// Retrieve an instance object from MongoDB.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public T Get<T>(string key, object value)
        {
            var type = typeof(T);
            if (string.IsNullOrEmpty(key)) { key = "_id"; }
            
            string collectionName = PluralizationService
                                   .CreateService(System.Globalization.CultureInfo.GetCultureInfo("en-US"))
                                   .Pluralize(type.Name.ToLower());
            var collection = _db.GetCollection<BsonDocument>(collectionName);

            var filter = Builders<BsonDocument>.Filter.Eq(key, value);

            var doc = collection.Find(filter).FirstOrDefaultAsync();
            doc.Wait();

            if (doc.Result == null) { return default(T); }

            return DeserializeBson<T>(doc.Result);
        }

        public List<T> GetList<T>(string key, object value)
        {
            var type = typeof(T);
            if (string.IsNullOrEmpty(key)) { key = "_id"; }
            
            string collectionName = PluralizationService
                                    .CreateService(System.Globalization.CultureInfo.GetCultureInfo("en-US"))
                                    .Pluralize(type.Name.ToLower());
            var collection = _db.GetCollection<BsonDocument>(collectionName);

            var filter = Builders<BsonDocument>.Filter.Eq(key, value);

            var docs = collection.Find(filter).ToListAsync();
            docs.Wait();

            if (docs.Result.Count > 0)
            {
                List<string> propertyNames = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList();
                return docs.Result.Select(d => DeserializeBson<T>(d, propertyNames)).ToList();
            }
            return new List<T>();
        }

        private T DeserializeBson<T>(BsonDocument bsonDoc, List<string> properyNames = null)
        {
            var type = typeof(T);

            if (properyNames == null)
            {
                properyNames = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList();
            }

            List<BsonElement> elements = new List<BsonElement>();
            foreach (var element in bsonDoc.Elements)
            {
                string titleCase = System.Globalization.CultureInfo.GetCultureInfo("en-US").TextInfo.ToTitleCase(element.Name);
                if (properyNames.Contains(titleCase))
                {
                    elements.Add(new BsonElement(titleCase, element.Value));
                }
                else if (properyNames.Contains(element.Name))
                {
                    elements.Add(element);
                }
            }

            BsonDocument doc = new BsonDocument(elements);

            return BsonSerializer.Deserialize<T>(doc);
        }

        #endregion

        #region Insert

        /// <summary>
        /// Insert an entity
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entityToInsert"></param>
        /// <returns>ObjectId of the inserted entity</returns>
        public string Insert<T>(T entityToInsert)
        {
            string collectionName = PluralizationService
                                    .CreateService(System.Globalization.CultureInfo.GetCultureInfo("en-US"))
                                    .Pluralize(typeof(T).Name.ToLower());
            var collection = _db.GetCollection<BsonDocument>(collectionName);

            BsonDocument bsonDoc = entityToInsert.ToBsonDocument();
            var insertTask = collection.InsertOneAsync(bsonDoc);
            insertTask.Wait();

            return bsonDoc.Elements.FirstOrDefault(e => e.Value.IsObjectId).Value.ToString();
        }

        #endregion

        #region Delete

        /// <summary>
        /// Delete only one document
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>success: true, fail: false</returns>
        public bool DeleteOne<T>(string key, object value)
        {
            var type = typeof(T);
            if (string.IsNullOrEmpty(key)) { key = "_id"; }

            string collectionName = PluralizationService
                                   .CreateService(System.Globalization.CultureInfo.GetCultureInfo("en-US"))
                                   .Pluralize(type.Name.ToLower());
            var collection = _db.GetCollection<BsonDocument>(collectionName);
            var filter = Builders<BsonDocument>.Filter.Eq(key, value);

            var deleteTask = collection.DeleteOneAsync(filter);
            deleteTask.Wait();
            return (deleteTask.Result.DeletedCount == 1);
        }

        /// <summary>
        /// Delete multiple documents
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>Number of deleted documents</returns>
        public long DeleteMany<T>(string key, object value)
        {
            var type = typeof(T);
            if (string.IsNullOrEmpty(key)) { key = "_id"; }

            string collectionName = PluralizationService
                                   .CreateService(System.Globalization.CultureInfo.GetCultureInfo("en-US"))
                                   .Pluralize(type.Name.ToLower());
            var collection = _db.GetCollection<BsonDocument>(collectionName);
            var filter = Builders<BsonDocument>.Filter.Eq(key, value);

            var deleteTask = collection.DeleteManyAsync(filter);
            deleteTask.Wait();
            return deleteTask.Result.DeletedCount;
        }

        #endregion
    }
}
