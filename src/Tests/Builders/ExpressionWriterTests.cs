﻿using System;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using FluentAssertions;
using Tests.Data;
using Xunit;
using SqlFu.Builders;
using SqlFu.Providers;
using System.Linq;
using SqlFu.Configuration;
using System.Collections.Generic;
using FakeItEasy;
using SqlFu;
using SqlFu.Builders.Expressions;
using Tests._Fakes;

namespace Tests.Builders
{
    public class MyWriter : ExpressionVisitor
    {
        StringBuilder _sb;

        private readonly IDbProviderExpressions _provider;
        private readonly ITableInfoFactory _factory;
        private readonly IEscapeIdentifier _escape;


        public MyWriter(IDbProviderExpressions provider,ITableInfoFactory factory,IEscapeIdentifier escape)
        {
            _sb =  new StringBuilder();

            _provider = provider;
            _factory = factory;
            _escape = escape;
        }

        public ParametersManager Parameters { get; } = new ParametersManager();

        //string GetColumnName(LambdaExpression member)
        //{
        //    var mbody = member.Body as MemberExpression;
        //    if (mbody != null) return GetColumnName(mbody);
        //    var ubody = member.Body as UnaryExpression;
        //    if (ubody == null) throw new NotSupportedException("Only members and unary expressions are supported");
        //    return GetColumnName(ubody);
        //}

        private string GetColumnName(UnaryExpression member)
        {
            var mbody = member.Operand as MemberExpression;
            if (mbody != null) return GetColumnName(mbody);
            var ubody = member.Operand as UnaryExpression;
            if (ubody != null) return GetColumnName(ubody);
            throw new NotSupportedException("Only members and unary expressions are supported");
        }


        private string GetColumnName(MemberInfo column)
        {
            return _factory.GetInfo(column.DeclaringType).GetColumnName(column.Name, _escape);
        }


        private string GetColumnName(MemberExpression member)
        {
            var tableType = member.Expression.Type;
            var info = _factory.GetInfo(tableType);
            return info.GetColumnName(member, _escape);
        }


      

        //private Expression EqualityFromUnary(UnaryExpression node)
        //    => Expression.Equal(node.Operand, Expression.Constant(node.NodeType != ExpressionType.Not));

        //private Expression EqualityFromBoolProperty(Expression left, bool value)
        //    => Expression.Equal(left, Expression.Constant(value));

        public override string ToString() => _sb.ToString();


        bool IsSingleBooleanProperty(Expression node)
            =>!_columnMode && (node.Type.Is<bool>() || node.Type.Is<bool?>()) && node.NodeType == ExpressionType.MemberAccess;

        bool IsSingleBooleanConstant(ConstantExpression node)
            => node != null && node.Type.Is<bool>();
        

        protected override Expression VisitBinary(BinaryExpression node)
        {
            string op = "";
            switch (node.NodeType)
            {
                case ExpressionType.AndAlso:
                    op = "and";
                    break;
                case ExpressionType.OrElse:
                    op = "or";
                    break;
                case ExpressionType.Equal:
                    if (node.Right.IsNullUnaryOrConstant())
                    {
                        op = "is";
                        break;
                    }
                    if (IsSingleBooleanProperty(node.Left))
                    {
                        HandleSingleBooleanProperty(node.Left as MemberExpression,(bool)node.Right.GetValue());
                        return node;
                    }

                    op = "=";
                    break;
                case ExpressionType.GreaterThan:
                    op = ">";
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    op = ">=";
                    break;
                case ExpressionType.LessThan:
                    op = "<";
                    break;
                case ExpressionType.LessThanOrEqual:
                    op = "<=";
                    break;
                case ExpressionType.NotEqual:

                    if (node.Right.IsNullUnaryOrConstant())
                    {
                        op = "is not";
                        break;
                    }
                    if (IsSingleBooleanProperty(node.Left))
                    {
                        HandleSingleBooleanProperty(node.Left as MemberExpression, false);
                        return node;
                    }
                    op = "<>";
                    break;
                case ExpressionType.Add:
                    op = "+";
                    break;
                case ExpressionType.Subtract:
                    op = "-";
                    break;
                case ExpressionType.Multiply:
                    op = "*";
                    break;
                case ExpressionType.Divide:
                    op = "/";
                    break;

                default:
                    throw new NotSupportedException();
            }
            _sb.Append("(");
            Visit(node.Left);
          
            _sb.Append($" {op} ");
            Visit(node.Right);
            
            _sb.Append(")");
            return node;
        }

       
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value == null)
            {
                _sb.Append("null");
                return node;
            }



            WriteParameter(node);

            return node;
        }

        /// <summary>
        /// This is called only in criterias. It shouldn't be called when in generating columns
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.InvolvesParameter())
            {
                HandleParameter(node);
            }
            else
            {
                WriteParameter(node);               
            }
            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.InvolvesParameter())
            {
                HandleParameter(node);
            }
            else
            {
                _sb.Append("@" + Parameters.CurrentIndex);
                Parameters.AddValues(node.GetValue());
            }
            return node;
        }

      

        private void WriteParameter(Expression node)
        {
            _sb.Append("@" + Parameters.CurrentIndex);
            Parameters.AddValues(node.GetValue());
        }

        /// <summary>
        /// Names of the methods used to check if a column is contained by a collection
        /// </summary>
        static readonly string[] containsNames = new[] {"Contains", "HasValueIn"};

        private void HandleParameter(MethodCallExpression node)
        {
            
            if (node.HasParameterArgument())
            {
                if (containsNames.Contains(node.Method.Name))
                {
                    HandleContains(node);
                    return;
                }               

               
           // _provider.WriteMethodCall(node, _sb, _helper);
            }

            if (node.BelongsToParameter() && node.Object.Type.Is<string>())
            {
              HandleParamStringFunctions(node);            
            }
        }

        private void HandleParamStringFunctions(MethodCallExpression node)
        {
            var name = GetColumnName(node.Object as MemberExpression);

            object firstArg = null;
            if (node.Arguments.HasItems())
            {
                firstArg = node.Arguments[0].GetValue();
            }          
            string value = "";
            switch (node.Method.Name)
            {
                case "StartsWith":
                    value = $"{name} like @{Parameters.CurrentIndex}";
                    Parameters.AddValues(firstArg + "%");
                    break;
                case "EndsWith":
                    value = $"{name} like @{Parameters.CurrentIndex}";
                    Parameters.AddValues("%" + firstArg);
                    break;
                case "Contains":
                    value = $"{name} like @{Parameters.CurrentIndex}";
                    Parameters.AddValues("%" + firstArg + "%");
                    break;
                case "ToUpper":
                case "ToUpperInvariant":
                    value = _provider.ToUpper(name);
                    break;
                case "ToLower":
                case "ToLowerInvariant":
                    value = _provider.ToLower(name);
                    break;
                case "Substring":
                    value = _provider.Substring(name, (int)firstArg, (int)node.Arguments[1].GetValue());
                    break;
            }

            _sb.Append(value);
        }

        private void HandleContains(MethodCallExpression meth)
        {
            IList list = null;
            var colIdx = 1;
            
            if (meth.Arguments.Count == 1)
            {
                list = meth.Object.GetValue() as IList;
                colIdx = 0;
            }
            else
            {
                var valIdx = meth.Arguments[0].BelongsToParameter()?1:0;
                colIdx = valIdx == 0 ? 1 : 0;
                list = meth.Arguments[valIdx].GetValue() as IList;
                if (list == null)
                {
                    var en = meth.Arguments[valIdx].GetValue() as IEnumerable;
                    var lst = new ArrayList();
                    foreach (var d in en) lst.Add(d);
                    list = lst;
                }
               
                
            }

            //if (list == null)
            //{
            //    throw new NotSupportedException("Contains must be invoked on a IList (array or List)");
            //}

            if (list.Count > 0)
            {
                var param = meth.Arguments[colIdx] as MemberExpression;
                _sb.Append(GetColumnName(param));

                _sb.AppendFormat(" in (@{0})", Parameters.CurrentIndex);
                Parameters.AddValues(list);
            }
            else
            {
                _sb.Append("1 = 0");
            }
        }

      
        private void HandleParameter(MemberExpression node)
        {
            if (!_columnMode && node.Type == typeof(bool))
            {
                HandleSingleBooleanProperty(node, true);
                return;              
            }
            
                if (node.Expression.NodeType == ExpressionType.Parameter)
                {
                    _sb.Append(GetColumnName(node));
                }
                else
                {
                    HandleParameterSubProperty(node);
                }
               
            
          
        }

        private void HandleSingleBooleanProperty(MemberExpression node, bool b)
        {
            _sb.Append(GetColumnName(node)).Append("=");
            VisitConstant(Expression.Constant(b));
        }


        /// <summary>
        /// For properties of a parameter property.
        /// Used to for properties that can be translated into db functions
        /// </summary>
        /// <param name="node"></param>
        private void HandleParameterSubProperty(MemberExpression node)
        {
            if (node.Expression.Type == typeof(string))
            {
                HandleStringProperties(node);
                return;
            }

            if (node.Expression.Type == typeof(DateTime) || node.Expression.Type == typeof(DateTimeOffset))
            {
                HandleDateTimeProperties(node);
                return;
            }
        }


        private void HandleStringProperties(MemberExpression node)
        {
            var name = (node.Expression as MemberExpression).Member.Name;
            switch (node.Member.Name)
            {
                case "Length":
                    _sb.Append(_provider.Length(name));
                    break;
            }
        }

        private void HandleDateTimeProperties(MemberExpression node)
        {
            var name = node.Expression.GetPropertyName();
            switch (node.Member.Name)
            {
                case "Year":
                    _sb.Append(_provider.Year(name));
                    break;
                case "Day":
                    _sb.Append(_provider.Day(name));
                    break;
            }
        }


        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            if (!_columnMode) return node;

            HandleObject(node.NewExpression,node.Bindings.Select(d=>d.Member.Name).ToArray());
            _sb.Append(',');
            foreach (var arg in node.Bindings.Cast<MemberAssignment>())
            {
                
                Visit(arg.Expression);
                _sb.AppendFormat(" as {0},", arg.Member.Name);

            }
            _sb.RemoveLast();
            return node;
        }

        private void VisitProjection(NewExpression node)
        {
            if (node.Type.CheckIfAnonymousType())
            {
                HandleAnonymous(node);
                return;
            }

            HandleObject(node);
        }

        void HandleObject(NewExpression node,params string[] skip)
        {

            node.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(d=>!skip.Any(s=>d.Name==s))
                .ForEach(n =>
                {
                  _sb
                    //.AppendLine()
                    .Append(GetColumnName(n)).Append(",");

                });
            _sb.RemoveLast();
        }

        private void HandleAnonymous(NewExpression node)
        {
            var i = 0;
            foreach (var arg in node.Arguments)
            {
                //_sb.AppendLine();
                Visit(arg);
                _sb.AppendFormat(" as {0},", node.Members[i].Name);
                i++;
            }
            _sb.RemoveLast();
        }

        protected override Expression VisitNew(NewExpression node)
        {
            VisitProjection(node);        
            return node;
        }

        void HandleSingleBooleanConstant(ConstantExpression node)
        {
            var val = (bool)node.GetValue();
            _sb.Append(val ? "1=1" : "1<1");           
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            switch (node.NodeType)
            {
                case ExpressionType.Convert:
                    if (IsSingleBooleanProperty(node.Operand))
                    {
                        HandleSingleBooleanProperty(node.Operand as MemberExpression, true);
                        break;
                    }
                    var op = node.Operand as ConstantExpression;
                    if (IsSingleBooleanConstant(op))
                    {
                        HandleSingleBooleanConstant(op);
                        return node;
                    }
                    Visit(node.Operand);
                    break;
                case ExpressionType.New:

                    Visit(node.Operand);
                    break;
                

                case ExpressionType.Not:
                    if (node.Operand.BelongsToParameter())
                    {
                        if (IsSingleBooleanProperty(node.Operand))
                        {
                            HandleSingleBooleanProperty(node.Operand as MemberExpression, false);
                            break;
                        }
                    }

                    var opf = node.Operand as ConstantExpression;
                    if (IsSingleBooleanConstant(opf))
                    {
                        HandleSingleBooleanConstant(opf);
                        return node;
                    }
                    _sb.Append("not ");

                    Visit(node.Operand);
                    break;
            }

            return node;
        }

        public string GetSql(LambdaExpression expression)
        {
            _sb.Clear();
            _columnMode = false;
            Visit(expression);
            return _sb.ToString();
        }

        private bool _columnMode = false;

        public string GetColumnsSql(params LambdaExpression[] columns)
        {
            _sb.Clear();
            _columnMode = true;
            columns.ForEach(col =>
            {
                Visit(col.Body);               
                _sb.Append(",");

            });
            _sb.RemoveLast();
         
           return _sb.ToString();
           
        }

    }

    public class ExpressionWriterTests
    {
        private MyWriter _sut;
        Expression<Func<MapperPost, object>> _l;
        private IDbProviderExpressions _provider = A.Fake<IDbProviderExpressions>();

        public ExpressionWriterTests()
        {
            _sut = new MyWriter(_provider, Setup.InfoFactory(), FakeEscapeIdentifier.Instance);
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
            A.CallTo(() => _provider.Substring("Title", 0, 1)).Returns("sub(Title)");
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
            A.CallTo(() => _provider.Length("Title")).Returns("len(Title)");
            Get(d => d.Title.Length == 2).Should().Be("(len(Title) = @0)");
            
            FirstParameter.Should().Be(2);
        }

        [Fact]
        public void to_upper()
        {
            Get(d => d.Title.ToUpper());
            A.CallTo(()=>_provider.ToUpper("Title")).MustHaveHappened();
        }

        [Fact]
        public void to_lower()
        {
            Get(d => d.Title.ToLower());
            A.CallTo(()=>_provider.ToLower("Title")).MustHaveHappened();
        }

        [Fact]
        public void call_year_function_for_date()
        {
            Get(d => d.CreatedOn.Year);
            A.CallTo(()=>_provider.Year("CreatedOn")).MustHaveHappened();
        }

        [Fact]
        public void call_day_function_for_date()
        {
            Get(d => d.CreatedOn.Day);
            A.CallTo(()=>_provider.Day("CreatedOn")).MustHaveHappened();
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

        object FirstParameter => _sut.Parameters.ToArray().First();
        object Parameter(int i) => _sut.Parameters.ToArray().Skip(i).First();
    }

 
       
}