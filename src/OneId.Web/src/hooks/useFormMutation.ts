import { useMutation } from '@tanstack/react-query'
import type { UseMutationOptions } from '@tanstack/react-query'
import { toast } from 'sonner'
import { HTTPError } from 'ky'

type MutationMessages<TData = unknown, TVariables = unknown> = {
  success: string | ((data: TData, variables: TVariables) => string)
  error: string | ((err: unknown) => string)
  propagationNote?: boolean
  forceRevoke?: boolean
}

type UseFormMutationOptions<TData, TError, TVariables, TContext> = Omit<
  UseMutationOptions<TData, TError, TVariables, TContext>,
  'onSuccess' | 'onError'
> & {
  messages: MutationMessages<TData, TVariables>
  onSuccess?: (data: TData, variables: TVariables, context: TContext | undefined) => void
  onError?: (error: TError, variables: TVariables, context: TContext | undefined) => void
  onValidationError?: (errors: Record<string, string[]>) => void
}

export function useFormMutation<TData = unknown, TError = unknown, TVariables = void, TContext = unknown>(
  options: UseFormMutationOptions<TData, TError, TVariables, TContext>,
) {
  const { messages, onSuccess, onError, onValidationError, ...mutationOptions } = options

  return useMutation<TData, TError, TVariables, TContext>({
    ...mutationOptions,
    onSuccess: (data, variables, context) => {
      const successMsg =
        typeof messages.success === 'function'
          ? messages.success(data, variables)
          : messages.success

      if (messages.forceRevoke) {
        toast.success('User must re-authenticate — changes are immediate', { duration: Infinity })
      } else if (messages.propagationNote) {
        toast.success(successMsg, {
          description: 'Changes effective within 5 minutes.',
          duration: Infinity,
        })
      } else {
        toast.success(successMsg, { duration: Infinity })
      }

      onSuccess?.(data, variables, context)
    },
    onError: async (err, variables, context) => {
      if (err instanceof HTTPError) {
        const isClientError = err.response.status >= 400 && err.response.status < 500
        if (isClientError) {
          const body = await err.response.json().catch(() => null)
          if (body && typeof body === 'object' && 'errors' in body) {
            onValidationError?.(body.errors as Record<string, string[]>)
            onError?.(err, variables, context)
            return
          }
        }
      }

      const errorMsg =
        typeof messages.error === 'function' ? messages.error(err) : messages.error
      toast.error(errorMsg, { duration: 8000 })
      onError?.(err, variables, context)
    },
  })
}
