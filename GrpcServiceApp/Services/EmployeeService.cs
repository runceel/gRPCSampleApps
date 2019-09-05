using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcSampleApp;

namespace GrpcServiceApp.Services
{
    public class EmployeeService : GrpcSampleApp.Employees.EmployeesBase
    {
        private static List<Employee> Employees { get; } = new List<Employee>();
        public override Task<GetEmployeeReply> GetEmployees(Empty request, ServerCallContext context)
        {
            lock(Employees)
            {
                return Task.FromResult(new GetEmployeeReply
                {
                    Employees = { Employees }
                });
            }
        }

        public override Task<AddEmployeeReply> AddEmployee(Employee request, ServerCallContext context)
        {
            lock(Employees)
            {
                request.Id = Guid.NewGuid().ToString();
                Employees.Add(request);
                return Task.FromResult(new AddEmployeeReply
                {
                    Succeed = true,
                });
            }
        }

        public override Task<DeleteEmployeeReply> DeleteEmployee(DeleteEmployeeRequest request, ServerCallContext context)
        {
            lock(Employees)
            {
                var target = Employees.FirstOrDefault(x => x.Id == request.Id);
                if (target is null)
                {
                    return Task.FromResult(new DeleteEmployeeReply
                    {
                        Succeed = false,
                    });
                }

                Employees.Remove(target);
                return Task.FromResult(new DeleteEmployeeReply
                {
                    Succeed = true,
                });
            }
        }
    }
}
