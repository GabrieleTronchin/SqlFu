﻿using System;
using System.Linq.Expressions;
using FluentAssertions;
using Xunit;
using SqlFu.Providers;
using System.Linq;
using System.Collections.Generic;
using FakeItEasy;
using SqlFu;
using SqlFu.Builders;
using SqlFu.Builders.Expressions;
using Tests.TestData;
using Tests._Fakes;

namespace Tests.Builders
{

   
    public class ExpressionSqlGeneratorTests
    {
        private ExpressionSqlGenerator _sut;
        Expression<Func<MapperPost, object>> _l;
        private Fake<IDbProviderExpressions> _provider= new Fake<IDbProviderExpressions>();

   
        public ExpressionSqlGeneratorTests()
        {
            _provider.CallsTo(d => d.GetSql(A<MethodCallExpression>._, A<IGenerateSqlFromExpressions>._))
            .ReturnsLazily(x => TestDbProviderExpression.Instance.GetSql(x.GetArgument<MethodCallExpression>(0),
                x.GetArgument<IGenerateSqlFromExpressions>(1)));
            _sut = Setup.CreateExpressionSqlGenerator(_provider.FakedObject);
        }

        [Fact]
        public void single_constant_true()
        {
           _l = d => true;
            var sql = _sut.GetSql(_l);
            sql.Should().Be("1=1");
        }

        [Fact]
        public void single_constant_false()
        {
           _l = d => false;
            var sql = _sut.GetSql(_l);
            sql.Should().Be("1<1");
        }

        string Get(Expression<Func<MapperPost, object>> d) => _sut.GetSql(d);

        [Fact]
        public void single_constant_value()
        {
            var sql = Get(d => 2);
            sql.Should().Be("@0");
            _sut.Parameters.ToArray().Should().BeEquivalentTo(new[] {2});
        }

        [Fact]
        public void criteria_is_single_boolean_property()
        {
            var sql = Get(d => d.IsActive);
            sql.Should().Be("IsActive=@0");
            _sut.Parameters.ToArray().First().Should().Be(true);
        }
       
        #region ProjectColumns
        class IdName
        {
            public int Id { get; set; }
            public string Name { get; set; } 
        }

 
        [Fact]
        public void get_projection_from_new_object()
        {
            _l = d => new IdName();
            var sql = _sut.GetColumnsSql(_l);
            sql.Should().Be("Id,Name");
        }

         [Fact]
        public void get_projection_from_new_object_with_property_init()
        {
            _l = d => new IdName() {Name=d.Title};
            var sql = _sut.GetColumnsSql(_l);
            sql.Should().Be("Id,Title as Name");

             Get(_l).Should().BeEmpty();
        }



        [Fact]
        public void get_projection_from_anonymous()
        {
            _l = d => new { d.Id, Name = d.Title };
            var sql = _sut.GetColumnsSql(_l);
            sql.Should().Be("Id as Id,Title as Name");
        }

        [Fact]
        public void projection_with_column_calculation()
        {
            _l = d => new { d.Id, Name = d.SomeId+1 };
            var sql = _sut.GetColumnsSql(_l);
            sql.Should().Be("Id as Id,(SomeId + @0) as Name");
            FirstParameter.Should().Be(1);
        }

        #endregion

        #region Common criteria
        [Fact]
        public void criteria_single_boolean_property_is_negated()
        {
            var sql = Get(d => !d.IsActive);
            sql.Should().Be("IsActive=@0");
            _sut.Parameters.ToArray().First().Should().Be(false);
        }
        [Fact]
        public void property_as_column_name()
        {
            _l = d => d.IsActive;
            var sql = _sut.GetColumnsSql(_l);
            sql.Should().Be("IsActive");            
        }

        [Fact]
        public void simple_equality_criteria()
        {
            Get(d => d.SomeId == 24).Should().Be("(SomeId = @0)");
            FirstParameter.Should().Be(24);
            _sut.Parameters.Clear();
            var i = 24;
            Get(d => d.SomeId == i).Should().Be("(SomeId = @0)");
            FirstParameter.Should().Be(24);
        }
        [Fact]
         public void simple_inequality_criteria()
        {
            Get(d => d.SomeId != 24).Should().Be("(SomeId <> @0)");
            FirstParameter.Should().Be(24);
            _sut.Parameters.Clear();
            var i = 24;
            Get(d => d.SomeId != i).Should().Be("(SomeId <> @0)");
            FirstParameter.Should().Be(24);
        }

        [Fact]
        public void id_greater_than_12_and_less_than_24()
        {
            Get(p => p.SomeId > 12 && p.SomeId < 24).Should().Be("((SomeId > @0) and (SomeId < @1))");
            FirstParameter.Should().Be(12);
            Parameter(1).Should().Be(24);
        }

        [Fact]
        public void id_equals_field_or_title_is_null()
        {
            var d=Guid.Empty;
            Get(p => p.Id == d || p.Title == null).Should().Be("((Id = @0) or (Title is null))");
            FirstParameter.Should().Be(d);
        }


        [Fact]
        public void title_is_not_null()
        {
            Get(d => d.Title != null).Should().Be("(Title is not null)");
            _sut.Parameters.CurrentIndex.Should().Be(0);
        }

        [Fact]
        public void enum_handling()
        {
            Get(d => d.Order == SomeEnum.First).Should().Be("(Order = @0)");
            FirstParameter.Should().Be(SomeEnum.First);
            FirstParameter.Should().NotBe((int)SomeEnum.First);
            FirstParameter.Should().BeOfType<SomeEnum>();
        }

        [Fact]
        public void nullable_enum_when_null_handling()
        {
            Get(d => d.Order == null).Should().Be("(Order is null)");
            _sut.Parameters.CurrentIndex.Should().Be(0);
        }

        [Fact]
        public void id_and_isActive_not_true()
        {
            Get(d => d.SomeId == 23 && !d.IsActive).Should().Be("((SomeId = @0) and IsActive=@1)");
            FirstParameter.Should().Be(23);
            Parameter(1).Should().Be(false);
        }

        [Fact]
        public void id_and_isActive_is_true()
        {
            Get(d => d.SomeId == 23 && d.IsActive).Should().Be("((SomeId = @0) and IsActive=@1)");
            FirstParameter.Should().Be(23);
            Parameter(1).Should().Be(true);
        }

        [Fact]
        public void id_and_isActive_is_explicitely_true()
        {
            Get(d => d.SomeId == 23 && d.IsActive==true).Should().Be("((SomeId = @0) and IsActive=@1)");
            FirstParameter.Should().Be(23);
            Parameter(1).Should().Be(true);
        }

        [Fact]
        public void handle_nullable_boolean_property_true()
        {
            Get(d => d.IsBla).Should().Be("IsBla=@0");
            FirstParameter.Should().Be(true);

            _sut.Parameters.Clear();
            bool? b=true;
            Get(d => d.IsBla==b.Value).Should().Be("IsBla=@0");
            FirstParameter.Should().Be(true);

            _sut.Parameters.Clear();
            Get(d => d.IsBla==b).Should().Be("IsBla=@0");
            FirstParameter.Should().Be(true);
            
        }
         [Fact]
        public void handle_nullable_boolean_property_false()
        {
            Get(d => !d.IsBla).Should().Be("IsBla=@0");
            FirstParameter.Should().Be(false);
            
        }

        [Fact]
        public void handle_nullable_boolean_property_null()
        {
            Get(d => d.IsBla==null).Should().Be("(IsBla is null)");
        }

        [Fact]
        public void nullable_property_equality()
        {
            Get(p => p.Order == SomeEnum.Last).Should().Be("(Order = @0)");
            FirstParameter.Should().Be(SomeEnum.Last);
        }
        #endregion

        #region String functions

        [Fact]
        public void substring_of_column()
        {
            A.CallTo(() => _provider.FakedObject.Substring("Title", 0, 1)).Returns("sub(Title)");
            _l = d => d.Title.Substring(0, 1);
            Get(_l).Should().Be("sub(Title)");
            _sut.GetColumnsSql(_l).Should().Be("sub(Title)");
            _l = d => new {Name = d.Title.Substring(0, 1)};
            _sut.GetColumnsSql(_l).Should().Be("sub(Title) as Name");

        }

        [Fact]
        public void string_starts_with()
        {
            Get(d => d.Title.StartsWith("t")).Should().Be("Title like @0");
            FirstParameter.Should().Be("t%");
        }

        [Fact]
        public void string_ends_with()
        {
            Get(d => d.Title.EndsWith("t")).Should().Be("Title like @0");
            FirstParameter.Should().Be("%t");
        }

        [Fact]
        public void string_contains()
        {
            Get(d => d.Title.Contains("''t")).Should().Be("Title like @0");
            FirstParameter.Should().Be("%''t%");
        }

        [Fact]
        public void string_length()
        {
            A.CallTo(() => _provider.FakedObject.Length("Title")).Returns("len(Title)");
            Get(d => d.Title.Length == 2).Should().Be("(len(Title) = @0)");
            
            FirstParameter.Should().Be(2);
        }

        [Fact]
        public void to_upper()
        {
            Get(d => d.Title.ToUpper());
            A.CallTo(()=>_provider.FakedObject.ToUpper("Title")).MustHaveHappened();
        }

        [Fact]
        public void to_lower()
        {
            Get(d => d.Title.ToLower());
            A.CallTo(()=>_provider.FakedObject.ToLower("Title")).MustHaveHappened();
        }

        [Fact]
        public void call_year_function_for_date()
        {
            Get(d => d.CreatedOn.Year);
            A.CallTo(()=>_provider.FakedObject.Year("CreatedOn")).MustHaveHappened();
        }

        [Fact]
        public void call_day_function_for_date()
        {
            Get(d => d.CreatedOn.Day);
            A.CallTo(()=>_provider.FakedObject.Day("CreatedOn")).MustHaveHappened();
        }

        T Cast<T>(object o) => (T)o;

        [Fact]
        public void column_is_contained_in_ienumerable()
        {
            IEnumerable<string> val = new[] { "bula","strula" };
            Get(d => val.Contains(d.Title)).Should().Be("Title in (@0)");

            Cast<IEnumerable<string>>(FirstParameter).ShouldAllBeEquivalentTo(val);

            _sut.Parameters.Clear();
            Get(d => d.Title.HasValueIn(val)).Should().Be("Title in (@0)"); ;
            Cast<IEnumerable<string>>(FirstParameter).ShouldAllBeEquivalentTo(val);
        }

        [Fact]
        public void column_is_contained_in_array()
        {
            string[] val = new[] { "bula","strula" };
            Get(d => val.Contains(d.Title)).Should().Be("Title in (@0)");

            Cast<IEnumerable<string>>(FirstParameter).ShouldAllBeEquivalentTo(val);

            _sut.Parameters.Clear();
            Get(d => d.Title.HasValueIn(val)).Should().Be("Title in (@0)"); ;
            Cast<IEnumerable<string>>(FirstParameter).ShouldAllBeEquivalentTo(val);
        }

        [Fact]
        public void column_is_contained_in_list()
        {
            List<string> val = new List<string>(){ "bula","strula" };
            Get(d => val.Contains(d.Title)).Should().Be("Title in (@0)");

            Cast<IEnumerable<string>>(FirstParameter).ShouldAllBeEquivalentTo(val);

            _sut.Parameters.Clear();
            Get(d => d.Title.HasValueIn(val)).Should().Be("Title in (@0)"); ;
            Cast<IEnumerable<string>>(FirstParameter).ShouldAllBeEquivalentTo(val);
        }

        #endregion

        #region Common db functions

    

        [Fact]
        public void sum_of_column()
        {
            var sql = Get(d => d.Sum(d.IsActive));
            sql.Should().Be("sum(IsActive)");

            Get(d => d.Sum(d.SomeId + 6)).Should().Be("sum((SomeId + @0))");
            FirstParameter.Should().Be(6);
        }

        [Fact]
        public void max_of_column()
        {
            var sql = Get(d => d.Max(d.IsActive));
            sql.Should().Be("max(IsActive)");           
        }

        [Fact]
        public void avg_of_column()
        {
            var sql = Get(d => d.Avg(d.SomeId));
            sql.Should().Be("avg(SomeId)");           
        }

        [Fact]
        public void min_of_expression()
        {
            var sql = Get(d => d.Min(d.SomeId*5));
            sql.Should().Be("min((SomeId * @0))");
            FirstParameter.Should().Be(5);
        }

        [Fact]
        public void func_count()
        {
            var sql = Get(d =>d.Count(d.IsActive));
            sql.Should().Be("count(IsActive)");

            Get(d => d.Count()).Should().Be("count(*)");

            Get(d => new {total = d.Count()}).Should().Be("count(*) as total");
        }

        [Fact]
        public void concat_columns()
        {
            var sql = Get(d => d.Concat(d.SomeId,"g",d.Title));
            sql.Should().Be("concat(SomeId,@0,Title)");
            FirstParameter.Should().Be("g");
        }

        [Fact]
        public void round_columns()
        {
            var sql = Get(d => d.Round(d.SomeId,3));
            sql.Should().Be("round(SomeId,@0)");
            FirstParameter.Should().Be(3);
        }


        [Fact]
        public void floor_columns()
        {
            var sql = Get(d => d.Floor(d.SomeId));
            sql.Should().Be("floor(SomeId)");
            
        }

        [Fact]
        public void func_ceiling_columns()
        {
            var sql = Get(d => d.Ceiling(d.SomeId));
            sql.Should().Be("ceiling(SomeId)");
            
        }
        #endregion

        [Fact]
        public void inject_sql()
        {
            Get(d => d.Decimal == 23);
            var test = "bla";
            Get(d => d.InjectSql("bubu @id - @test", new {id = 3,test})).Should().Be("bubu @1 - @2");
            Parameter(1).Should().Be(3);
            Parameter(2).Should().Be("bla");
        }

        object FirstParameter => _sut.Parameters.ToArray().First();
        object Parameter(int i) => _sut.Parameters.ToArray().Skip(i).First();
    }

 
       
}