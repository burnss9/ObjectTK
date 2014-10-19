﻿#region License
// DerpGL License
// Copyright (C) 2013-2014 J.C.Bernack
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using DerpGL.Exceptions;
using OpenTK.Graphics.OpenGL;

namespace DerpGL.Queries
{
    /// <summary>
    /// Provides named queries of hardware counters.
    /// </summary>
    /// <typeparam name="T">An enum type containing the query names.</typeparam>
    public class QueryMapping<T>
        : QueryIndexer
        , IDisposable
        where T : struct, IConvertible
    {
        /// <summary>
        /// Represents an OpenGL query.
        /// </summary>
        private class QueryMap
            : GLResource
        {
            internal bool Active;
            internal QueryTarget Target;
            internal int Index;

            public int Value;
            public int Average;

            public QueryMap()
                : base(GL.GenQuery())
            {
            }

            protected override void Dispose(bool manual)
            {
                if (!manual) return;
                GL.DeleteQuery(Handle);
            }
        }


        /// <summary>
        /// Gets the average value measured for the given query name.
        /// </summary>
        /// <param name="key"></param>
        public float this[T key]
        {
            get
            {
                return _queries[key].Average;
            }
        }

        /// <summary>
        /// Elapsed time is measured in nanoseconds and divided by this factor for better readability.<br/>
        /// A factor of 1,000 therefore results in microseconds, a factor of 1,000,000 results in milliseconds.
        /// </summary>
        public int ElapsedTimeFactor = 1000;

        /// <summary>
        /// Weighting factor used for averaging.<br/>
        /// A value close to 1 enables very fast averaging, giving noisy results.<br/>
        /// A value close to 0 gives a better mean, reacting much slower to fluctuations in the results.<br/>
        /// The default value is 0.05f.<br/>
        /// Let the current and the previous query result be A and B, respectively, then the average is calculated with this formula:<br/>
        /// average = A * AveragingFactor + B * (1-AveragingFactor);<br/>
        /// </summary>
        public float AveragingFactor = 0.05f;

        /// <summary>
        /// Holds all QueryMap objects.
        /// </summary>
        private readonly Dictionary<T, QueryMap> _queries;

        /// <summary>
        /// Initializes a new instance of this QueryMapping and generates required query objects.
        /// </summary>
        public QueryMapping()
        {
            if (!typeof(T).IsEnum) throw new ArgumentException("T must be an enumerated type");
            // create a query object for each enum entry
            _queries = Enum.GetValues(typeof (T)).Cast<T>().ToDictionary(_ => _, _ => new QueryMap());
        }

        public void Dispose()
        {
            foreach (var queryMap in _queries.Values)
            {
                queryMap.Dispose();
            }
        }

        /// <summary>
        /// Begins the given query name.
        /// </summary>
        /// <param name="mapping">The query name to begin.</param>
        /// <param name="target">The query target to capture.</param>
        public void Begin(T mapping, QueryTarget target)
        {
            var map = _queries[mapping];
            if (map.Active) throw new QueryException(string.Format("Query already active: {0} {1}", target, mapping));
            map.Active = true;
            map.Target = target;
            map.Index = AcquireIndex(target);
            GL.BeginQueryIndexed(target, map.Index, map.Handle);
        }

        /// <summary>
        /// End the given query name.
        /// </summary>
        /// <param name="mapping">The query name to end.</param>
        public void End(T mapping)
        {
            var map = _queries[mapping];
            if (!map.Active) throw new QueryException(string.Format("Query not active: {0}", mapping));
            GL.EndQueryIndexed(map.Target, map.Index);
            ReleaseIndex(map.Target, map.Index);
            map.Active = false;
        }

        /// <summary>
        /// Updates all query results.
        /// </summary>
        public void Update()
        {
            foreach (var map in _queries.Values)
            {
                // get current value
                GL.GetQueryObject(map.Handle, GetQueryObjectParam.QueryResult, out map.Value);
                // scale elapsed time
                if (map.Target == QueryTarget.TimeElapsed) map.Value /= ElapsedTimeFactor;
                // calculate averaged value
                map.Average = (int)(map.Value * AveragingFactor + map.Average * (1 - AveragingFactor));
            }
        }

        /// <summary>
        /// Retrieves query results.
        /// </summary>
        /// <returns>The query results.</returns>
        public IEnumerable<KeyValuePair<T, int>> GetValues()
        {
            return _queries.Select(map => new KeyValuePair<T, int>(map.Key, map.Value.Value));
        }

        /// <summary>
        /// Retrieves averaged query results.
        /// </summary>
        /// <returns>The averaged query results.</returns>
        public IEnumerable<KeyValuePair<T, int>> GetAverages()
        {
            return _queries.Select(map => new KeyValuePair<T, int>(map.Key, map.Value.Average));
        }
    }
}