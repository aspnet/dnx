#include <mono/jit/jit.h>
#include <mono/metadata/assembly.h>
#include <mono/metadata/object.h>
#include <string>

static const char* s_managed_assembly_name = "klr.mono.managed.dll";

int main(int argc, char** argv)
{
    std::string app_base;
    std::string assembly_path;

    // REVIEW: Not reliable (figure out how to do this in a portable way)
    if(argc > 0) 
    {
        assembly_path = argv[0];
        app_base = assembly_path.substr(0, assembly_path.length() - 3);
        assembly_path = app_base + s_managed_assembly_name;
    }

    MonoDomain* domain = mono_jit_init ("klr");

    if(domain == NULL)
    {
        printf("Failed to create mono runtime\n");
        return -1;
    }

    MonoAssembly* assembly = mono_domain_assembly_open(domain, assembly_path.c_str());

    if(assembly == NULL)
    {
        printf("Unable to locate klr.mono.managed.dll\n");
        return -1;
    }

    MonoImage* image = mono_assembly_get_image(assembly);

    if(image == NULL)
    {
        printf("Unable to get mono image for klr.mono.managed.dll\n");
        return -1;
    }

    MonoClass* klass = mono_class_from_name(image, "", "EntryPoint");

    if(image == NULL)
    {
        printf("Unable to get class for EntryPoint\n");
        return -1;
    }

    MonoMethod* method = mono_class_get_method_from_name(klass, "Main", 2);
    if(method == NULL)
    {
        printf("Unable to find Main method\n");
        return -1;
    }

    MonoObject* exception;
    void* args[2];
    args[0] = &argc;
    args[1] = argv;
    mono_runtime_invoke(method, NULL, args, &exception);

    if(exception != NULL)
    {
        mono_print_unhandled_exception(exception);
        return -1;
    }

    return 0;
}
