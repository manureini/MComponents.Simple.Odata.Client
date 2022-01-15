﻿using Microsoft.AspNetCore.Components;
using MShared;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace MComponents.Simple.Odata.Client.Forms
{
    public class OdataModelFormReferenced<T, U> : OdataModelForm<U> where T : IIdentifiable where U : class, IIdentifiable, new()
    {
        [Parameter]
        public T Parent { get; set; }

        [Parameter]
        public string NavigationPropertyChildToParent { get; set; }

        [Parameter]
        public string NavigationPropertyParentToChild { get; set; }

        protected PropertyInfo mNavigationPropertyChildToParent;
        protected PropertyInfo mNavigationPropertyParentToChild;

        protected MFormSubmitArgs mLastSubmitArgs;

        override async protected Task OnInitializedAsync()
        {
            if (Id == null)
            {
                Model = new U();

                mNavigationPropertyChildToParent = typeof(U).GetProperty(NavigationPropertyChildToParent);

                if (!typeof(T).IsAssignableTo(mNavigationPropertyChildToParent.PropertyType))
                    throw new ArgumentException($"Parent type {typeof(T)} is not assignable to {mNavigationPropertyChildToParent.PropertyType} of {nameof(NavigationPropertyChildToParent)} {NavigationPropertyChildToParent}");

                mNavigationPropertyParentToChild = typeof(T).GetProperty(NavigationPropertyParentToChild);

                if (!typeof(U).IsAssignableTo(mNavigationPropertyParentToChild.PropertyType) && !typeof(ICollection<>).MakeGenericType(typeof(U)).IsAssignableTo(mNavigationPropertyParentToChild.PropertyType))
                    throw new ArgumentException($"Child type {typeof(U)} is not assignable to {mNavigationPropertyParentToChild.PropertyType} of {nameof(NavigationPropertyParentToChild)} {NavigationPropertyParentToChild}");

                mNavigationPropertyChildToParent.SetValue(Model, Parent);
                return;
            }

            await LoadModel();
        }

        public override Task OnSubmit(MFormSubmitArgs pArgs)
        {
            mLastSubmitArgs = pArgs;
            return Task.CompletedTask;
        }

        public async Task SubmitModel()
        {
            if (mLastSubmitArgs == null)
                return;

            await base.OnSubmit(mLastSubmitArgs);
            mLastSubmitArgs = null;

            if (typeof(U).IsAssignableTo(mNavigationPropertyParentToChild.PropertyType))
            {
                mNavigationPropertyParentToChild.SetValue(Parent, Model);
            }
            else
            {
                var collection = (ICollection<U>)mNavigationPropertyParentToChild.GetValue(Parent, null);

                if (collection == null)
                {
                    collection = new List<U>();
                    mNavigationPropertyParentToChild.SetValue(Parent, collection);
                }

                if (!collection.Contains(Model))
                    collection.Add(Model);
            }
        }

        protected override void OnParametersSet()
        {
            base.OnParametersSet();

            if (NavigationPropertyChildToParent == null)
                throw new ArgumentNullException(nameof(NavigationPropertyChildToParent));

            if (NavigationPropertyParentToChild == null)
                throw new ArgumentNullException(nameof(NavigationPropertyParentToChild));

            if (Parent == null)
                throw new ArgumentNullException(nameof(Parent));
        }
    }
}